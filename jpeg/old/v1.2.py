import numpy as np
import rawpy
from matplotlib import pyplot as plt

# Standard JPEG Luminance Quantization Matrix (8x8)
# Used to reduce the precision of high-frequency components (which the human eye is less sensitive to)
Q = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
])

# 1D Discrete Cosine Transform (DCT)
def dct_1d(vector):
    N = len(vector)
    result = np.zeros(N)
    for k in range(N):
        sum_val = 0
        for n in range(N):
            sum_val += vector[n] * np.cos(np.pi * k * (2*n + 1) / (2 * N))
        result[k] = sum_val * 2
    return result

# 2D DCT by applying 1D DCT on rows, then columns
def dct_2d(block):
    N = block.shape[0]
    temp = np.zeros((N, N))
    result = np.zeros((N, N))
    for i in range(N):
        temp[i, :] = dct_1d(block[i, :])
    for j in range(N):
        result[:, j] = dct_1d(temp[:, j])
    return result

# 1D Inverse DCT (IDCT)
def idct_1d(vector):
    N = len(vector)
    result = np.zeros(N)
    for n in range(N):
        sum_val = 0
        for k in range(N):
            sum_val += vector[k] * np.cos(np.pi * k * (2*n + 1) / (2 * N))
        result[n] = sum_val * 2 / N
    return result

# 2D IDCT by applying 1D IDCT on columns, then rows
def idct_2d(block):
    N = block.shape[0]
    temp = np.zeros((N, N))
    result = np.zeros((N, N))
    for j in range(N):
        temp[:, j] = idct_1d(block[:, j])
    for i in range(N):
        result[i, :] = idct_1d(temp[i, :])
    return result

# JPEG compression function using DCT and quantization
def jpeg_compress(image, block_size=8):
    h, w = image.shape
    compressed = np.zeros_like(image, dtype=np.float32)

    # Process image in non-overlapping blocks
    for i in range(0, h, block_size):
        for j in range(0, w, block_size):
            block = image[i:i+block_size, j:j+block_size] - 128  # Center values around 0
            dct_block = dct_2d(block)  # Apply 2D DCT
            quantized = np.round(dct_block / Q)  # Quantize using JPEG matrix
            compressed[i:i+block_size, j:j+block_size] = quantized
    return compressed

# JPEG decompression function using dequantization and inverse DCT
def jpeg_decompress(compressed, block_size=8):
    h, w = compressed.shape
    reconstructed = np.zeros_like(compressed, dtype=np.float32)

    # Process compressed blocks
    for i in range(0, h, block_size):
        for j in range(0, w, block_size):
            quantized = compressed[i:i+block_size, j:j+block_size]
            dequantized = quantized * Q  # Dequantize
            idct_block = idct_2d(dequantized) + 128  # Apply inverse DCT and shift back
            reconstructed[i:i+block_size, j:j+block_size] = np.clip(idct_block, 0, 255)
    return reconstructed.astype(np.uint8)


# Load a .NEF RAW image using rawpy and convert to RGB
with rawpy.imread('image.NEF') as raw:
    rgb = raw.postprocess()

# Convert RGB to grayscale using luminance formula (Y = 0.299R + 0.587G + 0.114B)
gray = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]
gray = gray.astype(np.float32)

# Apply JPEG compression and decompression pipeline
compressed = jpeg_compress(gray)
reconstructed = jpeg_decompress(compressed)

# Plot original and reconstructed images for comparison
plt.subplot(1, 2, 1)
plt.imshow(gray, cmap='gray')
plt.title('Original')

plt.subplot(1, 2, 2)
plt.imshow(reconstructed, cmap='gray')
plt.title('Reconstructed')

plt.show()