import numpy as np
from matplotlib import pyplot as plt
import imageio.v2 as imageio
import time
from mpi4py import MPI
from pathlib import Path
from scipy.ndimage import zoom

# === Global Block Size ===
block_size = 64  

# Initialize MPI
comm = MPI.COMM_WORLD
rank = comm.Get_rank()
size = comm.Get_size()

# Measure execution time on root
if rank == 0:
    start_time = time.perf_counter()

# === Step 1: Load image and convert to YCbCr (on root) ===
if rank == 0:
    img_path = Path("images/gato.png")
    rgb = imageio.imread(img_path).astype(np.float32)
else:
    rgb = None

# Broadcast image to all processes
rgb = comm.bcast(rgb, root=0)

# === RGB <-> YCbCr Conversion ===
def rgb_to_ycbcr(image):
    R, G, B = image[:, :, 0], image[:, :, 1], image[:, :, 2]
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

# === Step 2: Pad image to multiple of block_size ===
def pad_image(image, block_size):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

Y, Cb, Cr = rgb_to_ycbcr(rgb)
Y_pad, orig_h, orig_w = pad_image(Y, block_size)
Cb_pad, _, _ = pad_image(Cb, block_size)
Cr_pad, _, _ = pad_image(Cr, block_size)

# === Step 3: DCT and IDCT ===
def dct_matrix(N):
    C = np.zeros((N, N))
    for k in range(N):
        for n in range(N):
            alpha = np.sqrt(1/N) if k == 0 else np.sqrt(2/N)
            C[k, n] = alpha * np.cos(np.pi * (2*n + 1) * k / (2 * N))
    return C

C = dct_matrix(block_size)

def dct_2d_vec(block):
    return C @ block @ C.T

def idct_2d_vec(block):
    return C.T @ block @ C

# === Step 4: Quantization Matrices (scaled) ===
Q_Y_base = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
])

Q_C_base = np.array([
    [17,18,24,47,99,99,99,99],
    [18,21,26,66,99,99,99,99],
    [24,26,56,99,99,99,99,99],
    [47,66,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99]
])

def scale_quant_matrix(Q, target_size):
    zoom_factor = target_size / Q.shape[0]
    return np.round(zoom(Q, zoom_factor, order=1))

Q_Y = scale_quant_matrix(Q_Y_base, block_size)
Q_C = scale_quant_matrix(Q_C_base, block_size)

# === Step 5: Parallel DCT Compression (block-wise distribution) ===
def process_channel_parallel(channel, Q):
    h, w = channel.shape
    compressed = np.zeros_like(channel)

    # Create list of all block positions
    block_positions = [(i, j) for i in range(0, h, block_size)
                              for j in range(0, w, block_size)]
    
    # Distribute blocks among processes
    local_blocks = [block_positions[i] for i in range(len(block_positions)) if i % size == rank]

    for i, j in local_blocks:
        block = channel[i:i+block_size, j:j+block_size]
        if block.shape != (block_size, block_size):
            continue  # Skip incomplete blocks
        block -= 128
        dct_block = dct_2d_vec(block)
        quantized = np.round(dct_block / Q)
        dequantized = quantized * Q
        idct_block = idct_2d_vec(dequantized) + 128
        compressed[i:i+block_size, j:j+block_size] = idct_block

    # Reduce all partial results into full image
    full_compressed = np.zeros_like(channel)
    comm.Allreduce(compressed, full_compressed, op=MPI.SUM)
    return full_compressed

# === Step 6: Compress channels in parallel ===
Y_comp  = process_channel_parallel(Y_pad,  Q_Y)
Cb_comp = process_channel_parallel(Cb_pad, Q_C)
Cr_comp = process_channel_parallel(Cr_pad, Q_C)

# === Step 7: Crop and convert back to RGB (on root) ===
if rank == 0:
    Y_final  = Y_comp[:orig_h, :orig_w]
    Cb_final = Cb_comp[:orig_h, :orig_w]
    Cr_final = Cr_comp[:orig_h, :orig_w]

    rgb_final = ycbcr_to_rgb(Y_final, Cb_final, Cr_final)

    # Show execution time
    end_time = time.perf_counter()
    print(f"Total Execution Time: {end_time - start_time:.3f} seconds using {size} MPI processes")

    # Save output
    out_path = Path("outputs/mpi_blocksize.jpeg")
    out_path.parent.mkdir(exist_ok=True)
    plt.imsave(out_path, rgb_final.astype(np.uint8))
