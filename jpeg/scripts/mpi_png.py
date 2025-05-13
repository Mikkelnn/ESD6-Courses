import numpy as np
from matplotlib import pyplot as plt
import imageio.v2 as imageio
import time
from mpi4py import MPI
from pathlib import Path

# Initialize MPI
comm = MPI.COMM_WORLD
rank = comm.Get_rank()
size = comm.Get_size()

# Start measuring total execution time (only on root)
if rank == 0:
    start_time = time.perf_counter()

# Step 1: Load PNG image and convert to grayscale (only on root)
if rank == 0:
    img_path = Path("images/gato.png")
    rgb = imageio.imread(img_path).astype(np.float32)
    # Convert to grayscale using luminance formula
    gray = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]
else:
    gray = None

# Broadcast grayscale image to all processes
gray = comm.bcast(gray, root=0)

# Step 2: Pad image to multiple of 8
def pad_image(image, block_size=8):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

gray_padded, orig_h, orig_w = pad_image(gray)

# Step 3: DCT matrix
def dct_matrix(N=8):
    C = np.zeros((N, N))
    for k in range(N):
        for n in range(N):
            alpha = np.sqrt(1/N) if k == 0 else np.sqrt(2/N)
            C[k, n] = alpha * np.cos(np.pi * (2*n + 1) * k / (2 * N))
    return C

C = dct_matrix(8)

# Step 4: DCT and IDCT
def dct_2d_vec(block):
    return C @ block @ C.T

def idct_2d_vec(block):
    return C.T @ block @ C

# Step 5: Quantization matrix for luminance
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

# Step 6: Parallel compression
def process_channel_parallel(channel, Q):
    block_size = 8
    h, w = channel.shape
    compressed = np.zeros_like(channel)

    # Divide rows among processes
    rows_per_proc = h // size
    start_row = rank * rows_per_proc
    end_row = h if rank == size - 1 else (rank + 1) * rows_per_proc

    # Process assigned rows
    for i in range(start_row, end_row, block_size):
        for j in range(0, w, block_size):
            block = channel[i:i+block_size, j:j+block_size] - 128
            dct_block = dct_2d_vec(block)
            quantized = np.round(dct_block / Q)
            dequantized = quantized * Q
            idct_block = idct_2d_vec(dequantized) + 128
            compressed[i:i+block_size, j:j+block_size] = idct_block

    # Gather results from all processes
    full_compressed = np.zeros_like(channel)
    comm.Allreduce(compressed, full_compressed, op=MPI.SUM)
    return full_compressed

# Run compression
gray_compressed = process_channel_parallel(gray_padded, Q_Y)

# Step 7: Crop back to original size and save (only root)
if rank == 0:
    final_gray = gray_compressed[:orig_h, :orig_w]
    end_time = time.perf_counter()
    total_time = end_time - start_time
    print(f"Total Execution Time: {total_time:.3f} seconds using {size} MPI processes")

    out_path = Path("outputs/mpi_gray.jpeg")
    plt.imsave(out_path, final_gray.astype(np.uint8), cmap='gray')



