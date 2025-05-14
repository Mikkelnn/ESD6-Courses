import numpy as np
from matplotlib import pyplot as plt
import imageio
import time
from mpi4py import MPI

# Initialize MPI
comm = MPI.COMM_WORLD
rank = comm.Get_rank()
size = comm.Get_size()

# Load and Broadcast the Image
if rank == 0:
    rgb = imageio.v2.imread('gato.png').astype(np.float32)
else:
    rgb = None
rgb = comm.bcast(rgb, root=0)

# RGB ↔ YCbCr Functions
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

# Padding
def pad_image(image, block_size=8):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

Y_padded, orig_h, orig_w = pad_image(Y)
Cb_padded, _, _ = pad_image(Cb)
Cr_padded, _, _ = pad_image(Cr)

# DCT Matrices
def dct_matrix(N=8):
    C = np.zeros((N, N))
    for k in range(N):
        for n in range(N):
            alpha = np.sqrt(1/N) if k == 0 else np.sqrt(2/N)
            C[k, n] = alpha * np.cos(np.pi * (2*n + 1) * k / (2 * N))
    return C

C = dct_matrix(8)

def dct_2d_vec(block):
    return C @ block @ C.T

def idct_2d_vec(block):
    return C.T @ block @ C

# Quantization matrices
Q_Y = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
])

Q_C = np.array([
    [17,18,24,47,99,99,99,99],
    [18,21,26,66,99,99,99,99],
    [24,26,56,99,99,99,99,99],
    [47,66,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99]
])

def process_channel_parallel(channel, Q):
    block_size = 8
    h, w = channel.shape
    compressed = np.zeros_like(channel)
    rows_per_proc = h // size
    start_row = rank * rows_per_proc
    end_row = (rank + 1) * rows_per_proc if rank != size-1 else h

    for i in range(start_row, end_row, block_size):
        for j in range(0, w, block_size):
            block = channel[i:i+block_size, j:j+block_size] - 128
            dct_block = dct_2d_vec(block)
            quantized = np.round(dct_block / Q)
            dequantized = quantized * Q
            idct_block = idct_2d_vec(dequantized) + 128
            compressed[i:i+block_size, j:j+block_size] = idct_block

    full_compressed = np.zeros_like(channel)
    comm.Allreduce(compressed, full_compressed, op=MPI.SUM)
    return full_compressed

# Benchmark
num_runs = 10
times = []

for _ in range(num_runs):
    start = time.perf_counter()

    Y_compressed = process_channel_parallel(Y_padded, Q_Y)
    Cb_compressed = process_channel_parallel(Cb_padded, Q_C)
    Cr_compressed = process_channel_parallel(Cr_padded, Q_C)

    if rank == 0:
        Y_final = Y_compressed[:orig_h, :orig_w]
        Cb_final = Cb_compressed[:orig_h, :orig_w]
        Cr_final = Cr_compressed[:orig_h, :orig_w]

        final_rgb = ycbcr_to_rgb(Y_final, Cb_final, Cr_final)

    end = time.perf_counter()
    times.append(end - start)

# Results
if rank == 0:
    mean_time = np.mean(times)
    variance_time = np.var(times)
    print(f"✅ Benchmark Results ({num_runs} runs):")
    print(f"Mean execution time: {mean_time:.6f} seconds")
    print(f"Variance: {variance_time:.6e} seconds²")
