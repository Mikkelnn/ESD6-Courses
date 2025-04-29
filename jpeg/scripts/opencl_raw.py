import numpy as np
import pyopencl as cl
import imageio.v2 as imageio
from PIL import Image
import time
import rawpy  # Library to read raw images
from pathlib import Path

# ---------- Load image and convert to YCbCr ----------
def load_raw_image(filename):
    img_path = Path("images") / filename
    raw = rawpy.imread(str(img_path))  # rawpy requires a string path
    rgb = raw.postprocess()
    return rgb.astype(np.float32)
rgb = load_raw_image('image.nef')

R, G, B = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
Y  =  0.299 * R + 0.587 * G + 0.114 * B
Cb = -0.168736 * R - 0.331264 * G + 0.5 * B + 128
Cr =  0.5 * R - 0.418688 * G - 0.081312 * B + 128

def pad(image, N=8):
    h, w = image.shape
    pad_h = (N - h % N) % N
    pad_w = (N - w % N) % N
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

Yp, h, w = pad(Y)
Cbp, _, _ = pad(Cb)
Crp, _, _ = pad(Cr)

N = 8
Hp, Wp = Yp.shape
blocks_per_row = Wp // N
blocks_per_col = Hp // N
num_blocks = blocks_per_row * blocks_per_col

# ---------- JPEG Quantization Tables ----------
QY = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
], dtype=np.float32)

QC = np.array([
    [17,18,24,47,99,99,99,99],
    [18,21,26,66,99,99,99,99],
    [24,26,56,99,99,99,99,99],
    [47,66,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99]
], dtype=np.float32)

# ---------- OpenCL Setup ----------
platform = cl.get_platforms()[0]
device = platform.get_devices(cl.device_type.GPU)[0]
ctx = cl.Context([device])
queue = cl.CommandQueue(ctx)
mf = cl.mem_flags

# ---------- Kernel ----------
kernel_code = """
__kernel void dct_quant(__global float* input, __global float* output, __global float* Q, int N) {
    int block_id = get_global_id(0);
    int u = get_global_id(1) / N;
    int v = get_global_id(1) % N;

    float sum_val = 0.0f;
    for (int x = 0; x < N; x++) {
        for (int y = 0; y < N; y++) {
            float pixel = input[block_id * N*N + x*N + y];
            sum_val += pixel *
                       cos((float)M_PI*(2*x+1)*u/(2*N)) *
                       cos((float)M_PI*(2*y+1)*v/(2*N));
        }
    }
    float alpha_u = (u == 0) ? sqrt(1.0f/N) : sqrt(2.0f/N);
    float alpha_v = (v == 0) ? sqrt(1.0f/N) : sqrt(2.0f/N);
    float dct_coeff = alpha_u * alpha_v * sum_val;
    output[block_id * N*N + u*N + v] = round(dct_coeff / Q[u*N + v]);
}

__kernel void idct_dequant(__global float* input, __global float* output, __global float* Q, int N) {
    int block_id = get_global_id(0);
    int x = get_global_id(1) / N;
    int y = get_global_id(1) % N;

    float sum_val = 0.0f;
    for (int u = 0; u < N; u++) {
        for (int v = 0; v < N; v++) {
            float coeff = input[block_id * N*N + u*N + v] * Q[u*N + v];
            float alpha_u = (u == 0) ? sqrt(1.0f/N) : sqrt(2.0f/N);
            float alpha_v = (v == 0) ? sqrt(1.0f/N) : sqrt(2.0f/N);
            sum_val += alpha_u * alpha_v * coeff *
                       cos((float)M_PI*(2*x+1)*u/(2*N)) *
                       cos((float)M_PI*(2*y+1)*v/(2*N));
        }
    }
    output[block_id * N*N + x*N + y] = sum_val;
}

__kernel void rebuild(__global float* blocks, __global float* image, int N, int blocks_per_row) {
    int block_id = get_global_id(0);
    int local_idx = get_global_id(1);

    int local_x = local_idx / N;
    int local_y = local_idx % N;

    int block_row = block_id / blocks_per_row;
    int block_col = block_id % blocks_per_row;

    int img_x = block_row * N + local_x;
    int img_y = block_col * N + local_y;

    image[img_x * blocks_per_row * N + img_y] = blocks[block_id * N*N + local_x*N + local_y] + 128.0f;
}

__kernel void ycbcr_to_rgb(
    __global float* Y,
    __global float* Cb,
    __global float* Cr,
    __global uchar* RGB,
    int size)
{
    int i = get_global_id(0);
    float y  = Y[i];
    float cb = Cb[i] - 128.0f;
    float cr = Cr[i] - 128.0f;

    float r = y + 1.402f * cr;
    float g = y - 0.344136f * cb - 0.714136f * cr;
    float b = y + 1.772f * cb;

    RGB[i * 3 + 0] = clamp((int)r, 0, 255);
    RGB[i * 3 + 1] = clamp((int)g, 0, 255);
    RGB[i * 3 + 2] = clamp((int)b, 0, 255);
}
"""

program = cl.Program(ctx, kernel_code).build()

def run_channel(channel, Q):
    channel = channel - 128
    blocks = np.zeros((num_blocks, N*N), dtype=np.float32)
    idx = 0
    for i in range(0, Hp, N):
        for j in range(0, Wp, N):
            block = channel[i:i+N, j:j+N]
            blocks[idx] = block.flatten()
            idx += 1

    input_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=blocks)
    q_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=Q.flatten())
    dct_buf = cl.Buffer(ctx, mf.WRITE_ONLY, blocks.nbytes)
    program.dct_quant(queue, (num_blocks, N*N), None, input_buf, dct_buf, q_buf, np.int32(N))

    idct_buf = cl.Buffer(ctx, mf.WRITE_ONLY, blocks.nbytes)
    program.idct_dequant(queue, (num_blocks, N*N), None, dct_buf, idct_buf, q_buf, np.int32(N))

    out_img_buf = cl.Buffer(ctx, mf.WRITE_ONLY, Yp.nbytes)
    program.rebuild(queue, (num_blocks, N*N), None, idct_buf, out_img_buf, np.int32(N), np.int32(blocks_per_row))

    out_img = np.empty_like(Yp)
    cl.enqueue_copy(queue, out_img, out_img_buf)
    return out_img[:h, :w]

# ---------- Process Channels ----------
start = time.perf_counter()
Y_final  = run_channel(Yp, QY)
Cb_final = run_channel(Cbp, QC)
Cr_final = run_channel(Crp, QC)



# ---------- GPU YCbCr to RGB Conversion ----------
size = h * w
Y_buf  = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=Y_final.astype(np.float32).flatten())
Cb_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=Cb_final.astype(np.float32).flatten())
Cr_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=Cr_final.astype(np.float32).flatten())
RGB_buf = cl.Buffer(ctx, mf.WRITE_ONLY, size * 3)

end = time.perf_counter()
print(f"✅ Full GPU color compression and decompression in {end - start:.4f} seconds")


program.ycbcr_to_rgb(queue, (size,), None, Y_buf, Cb_buf, Cr_buf, RGB_buf, np.int32(size))

rgb_flat = np.empty(size * 3, dtype=np.uint8)
cl.enqueue_copy(queue, rgb_flat, RGB_buf)
rgb_out = rgb_flat.reshape((h, w, 3))

output_path = Path("outputs/opencl_png.jpeg")
Image.fromarray(rgb_out).save('opencl_raw.jpg', quality=85)
