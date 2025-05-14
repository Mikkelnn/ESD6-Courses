import numpy as np
import pyopencl as cl
import imageio
from matplotlib import pyplot as plt
import time

# Load the image and convert to grayscale (luminance channel)
rgb = imageio.v2.imread('gato.png').astype(np.float32)
gray = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]

# Padding to multiples of 8
def pad_image(image, block_size=8):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

gray_padded, orig_h, orig_w = pad_image(gray)
H, W = gray_padded.shape
N = 8

# Create 8x8 blocks
blocks_per_row = W // N
blocks_per_col = H // N
num_blocks = blocks_per_row * blocks_per_col

blocks = np.zeros((num_blocks, N, N), dtype=np.float32)
index = 0
for i in range(0, H, N):
    for j in range(0, W, N):
        blocks[index] = gray_padded[i:i+N, j:j+N] - 128
        index += 1

blocks_flat = blocks.reshape(num_blocks, N*N)

# JPEG Quantization Table
Q = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
], dtype=np.float32)

Q_flat = Q.flatten()

# OpenCL setup
platforms = cl.get_platforms()
gpu_devices = platforms[0].get_devices(device_type=cl.device_type.GPU)
ctx = cl.Context(devices=gpu_devices)
queue = cl.CommandQueue(ctx)

# OpenCL kernel for DCT + Quantization
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

# Build and create buffers
program = cl.Program(ctx, kernel_code).build()
mf = cl.mem_flags

input_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=blocks_flat)
quant_table_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=Q_flat)
dct_quantized_buf = cl.Buffer(ctx, mf.WRITE_ONLY, blocks_flat.nbytes)

# Launch DCT + Quantization
start_time = time.perf_counter()
program.dct_quant(queue, (num_blocks, N*N), None, input_buf, dct_quantized_buf, quant_table_buf, np.int32(N))
queue.finish()
end_time = time.perf_counter()

print(f"✅ GPU DCT + Quantization Time: {(end_time - start_time):.4f} seconds")

# Now decompress: IDCT + Dequantization
output_buf = cl.Buffer(ctx, mf.WRITE_ONLY, blocks_flat.nbytes)
program.idct_dequant(queue, (num_blocks, N*N), None, dct_quantized_buf, output_buf, quant_table_buf, np.int32(N))
queue.finish()

# Copy back final result
reconstructed_flat = np.empty_like(blocks_flat)
cl.enqueue_copy(queue, reconstructed_flat, output_buf)
queue.finish()

# Rebuild the image
reconstructed_blocks = reconstructed_flat.reshape(num_blocks, N, N)
reconstructed = np.zeros((H, W), dtype=np.float32)

index = 0
for i in range(0, H, N):
    for j in range(0, W, N):
        reconstructed[i:i+N, j:j+N] = np.clip(reconstructed_blocks[index] + 128, 0, 255)
        index += 1

# Crop to original size
final_image = reconstructed[:orig_h, :orig_w].astype(np.uint8)

# Save
plt.imsave('gpu_full_pipeline.png', final_image, cmap='gray')
