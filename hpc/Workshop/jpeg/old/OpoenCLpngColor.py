import numpy as np
import pyopencl as cl
import imageio
from matplotlib import pyplot as plt
import time
import rawpy  # Library to read raw images


# Load the color image
rgb = imageio.v2.imread('gato.png').astype(np.float32)

# Convert RGB to YCbCr
def rgb_to_ycbcr(image):
    R = image[:, :, 0]
    G = image[:, :, 1]
    B = image[:, :, 2]
    Y  =  0.299 * R + 0.587 * G + 0.114 * B
    Cb = -0.168736 * R - 0.331264 * G + 0.5 * B + 128
    Cr =  0.5 * R - 0.418688 * G - 0.081312 * B + 128
    return Y, Cb, Cr

def ycbcr_to_rgb(Y, Cb, Cr):
    R = Y + 1.402 * (Cr - 128)
    G = Y - 0.344136 * (Cb - 128) - 0.714136 * (Cr - 128)
    B = Y + 1.772 * (Cb - 128)
    rgb = np.stack((R, G, B), axis=-1)
    return np.clip(rgb, 0, 255).astype(np.uint8)

Y, Cb, Cr = rgb_to_ycbcr(rgb)

# Padding function
def pad_image(image, block_size=8):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

# Pad each channel
Y_padded, orig_h, orig_w = pad_image(Y)
Cb_padded, _, _ = pad_image(Cb)
Cr_padded, _, _ = pad_image(Cr)

H, W = Y_padded.shape
N = 8
blocks_per_row = W // N
blocks_per_col = H // N
num_blocks = blocks_per_row * blocks_per_col

# Create blocks for each channel
def create_blocks(channel):
    blocks = np.zeros((num_blocks, N, N), dtype=np.float32)
    index = 0
    for i in range(0, H, N):
        for j in range(0, W, N):
            blocks[index] = channel[i:i+N, j:j+N] - 128
            index += 1
    return blocks.reshape(num_blocks, N*N)

Y_blocks = create_blocks(Y_padded)
Cb_blocks = create_blocks(Cb_padded)
Cr_blocks = create_blocks(Cr_padded)

# Quantization tables
Q_Y = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
], dtype=np.float32)

Q_C = np.array([
    [17,18,24,47,99,99,99,99],
    [18,21,26,66,99,99,99,99],
    [24,26,56,99,99,99,99,99],
    [47,66,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99]
], dtype=np.float32)

Q_Y_flat = Q_Y.flatten()
Q_C_flat = Q_C.flatten()

# OpenCL setup
platforms = cl.get_platforms()
gpu_devices = platforms[0].get_devices(device_type=cl.device_type.GPU)
ctx = cl.Context(devices=gpu_devices)
queue = cl.CommandQueue(ctx)

# OpenCL kernel
kernel_code = """
__kernel void dct_quant(
    __global float* input,
    __global float* output,
    __global float* Q,
    const int N)
{
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

__kernel void idct_dequant(
    __global float* input,
    __global float* output,
    __global float* Q,
    const int N)
{
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
"""

program = cl.Program(ctx, kernel_code).build()
mf = cl.mem_flags

def process_channel(blocks_flat, Q_flat):
    input_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=blocks_flat)
    quant_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=Q_flat)
    compressed_buf = cl.Buffer(ctx, mf.WRITE_ONLY, blocks_flat.nbytes)

    program.dct_quant(queue, (num_blocks, N*N), None, input_buf, compressed_buf, quant_buf, np.int32(N))
    queue.finish()

    # IDCT
    output_buf = cl.Buffer(ctx, mf.WRITE_ONLY, blocks_flat.nbytes)
    program.idct_dequant(queue, (num_blocks, N*N), None, compressed_buf, output_buf, quant_buf, np.int32(N))
    queue.finish()

    result = np.empty_like(blocks_flat)
    cl.enqueue_copy(queue, result, output_buf)
    queue.finish()
    return result

start_time = time.perf_counter()

Y_recon_flat = process_channel(Y_blocks, Q_Y_flat)
Cb_recon_flat = process_channel(Cb_blocks, Q_C_flat)
Cr_recon_flat = process_channel(Cr_blocks, Q_C_flat)

end_time = time.perf_counter()
print(f"✅ Full GPU compression + decompression time: {(end_time - start_time):.4f} seconds")

# Rebuild image from blocks
def rebuild_image(flat_blocks):
    blocks = flat_blocks.reshape(num_blocks, N, N)
    image = np.zeros((H, W), dtype=np.float32)
    idx = 0
    for i in range(0, H, N):
        for j in range(0, W, N):
            image[i:i+N, j:j+N] = np.clip(blocks[idx] + 128, 0, 255)
            idx += 1
    return image

Y_final = rebuild_image(Y_recon_flat)[:orig_h, :orig_w]
Cb_final = rebuild_image(Cb_recon_flat)[:orig_h, :orig_w]
Cr_final = rebuild_image(Cr_recon_flat)[:orig_h, :orig_w]

# Convert back to RGB
final_rgb = ycbcr_to_rgb(Y_final, Cb_final, Cr_final)

# Save the final color image
plt.imsave('gpu_final_color.png', final_rgb)
