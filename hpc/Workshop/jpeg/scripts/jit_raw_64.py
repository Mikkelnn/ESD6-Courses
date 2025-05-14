import numpy as np
from matplotlib import pyplot as plt
import imageio
from numba import jit
from pathlib import Path
import rawpy  # Library to read raw images


# === Parameters ===
BLOCK_SIZE = 64  # <-- Change this to 8, 16, 32, 64, etc.

# === Load image and convert to YCbCr ===
def load_raw_image(filename):
    img_path = Path("images") / filename
    raw = rawpy.imread(str(img_path))  # rawpy requires a string path
    rgb = raw.postprocess()
    return rgb.astype(np.float32)
rgb = load_raw_image('image.nef')


def rgb_to_ycbcr(image):
    R, G, B = image[..., 0], image[..., 1], image[..., 2]
    Y  =  0.299 * R + 0.587 * G + 0.114 * B
    Cb = -0.168736 * R - 0.331264 * G + 0.5 * B + 128
    Cr =  0.5 * R - 0.418688 * G - 0.081312 * B + 128
    return Y, Cb, Cr

def ycbcr_to_rgb(Y, Cb, Cr):
    R = Y + 1.402 * (Cr - 128)
    G = Y - 0.344136 * (Cb - 128) - 0.714136 * (Cr - 128)
    B = Y + 1.772 * (Cb - 128)
    return np.clip(np.stack((R, G, B), axis=-1), 0, 255).astype(np.uint8)

Y, Cb, Cr = rgb_to_ycbcr(rgb)

# === Padding ===
def pad_image(image, block_size):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

Y_padded, orig_h, orig_w = pad_image(Y, BLOCK_SIZE)
Cb_padded, _, _ = pad_image(Cb, BLOCK_SIZE)
Cr_padded, _, _ = pad_image(Cr, BLOCK_SIZE)

# === DCT matrix generator ===
def create_dct_matrix(N):
    C = np.zeros((N, N), dtype=np.float32)
    for k in range(N):
        for n in range(N):
            alpha = np.sqrt(1 / N) if k == 0 else np.sqrt(2 / N)
            C[k, n] = alpha * np.cos(np.pi * (2 * n + 1) * k / (2 * N))
    return C

DCT_MAT = create_dct_matrix(BLOCK_SIZE)

# === Base Quantization matrices (8x8) ===
BASE_Q_Y = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
], dtype=np.float32)

BASE_Q_C = np.array([
    [17,18,24,47,99,99,99,99],
    [18,21,26,66,99,99,99,99],
    [24,26,56,99,99,99,99,99],
    [47,66,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99]
], dtype=np.float32)

# === Extend quantization matrices to BLOCK_SIZE ===
def expand_quant_matrix(Q_base, block_size):
    reps = (block_size // 8, block_size // 8)
    return np.tile(Q_base, reps)

Q_Y = expand_quant_matrix(BASE_Q_Y, BLOCK_SIZE)
Q_C = expand_quant_matrix(BASE_Q_C, BLOCK_SIZE)

# === DCT & IDCT with matrix multiplication ===
@jit(nopython=True)
def dct2(block, C):
    return C @ block @ C.T

@jit(nopython=True)
def idct2(block, C):
    return C.T @ block @ C

# === JPEG-like processing for a single channel ===
def process_channel_variable_block(channel, Q, C, block_size):
    h, w = channel.shape
    output = np.empty_like(channel)
    for i in range(0, h, block_size):
        for j in range(0, w, block_size):
            block = channel[i:i+block_size, j:j+block_size] - 128
            dct_block = dct2(block, C)
            quant = np.round(dct_block / Q)
            dequant = quant * Q
            idct_block = idct2(dequant, C) + 128
            output[i:i+block_size, j:j+block_size] = idct_block
    return output

Y_compressed = process_channel_variable_block(Y_padded, Q_Y, DCT_MAT, BLOCK_SIZE)
Cb_compressed = process_channel_variable_block(Cb_padded, Q_C, DCT_MAT, BLOCK_SIZE)
Cr_compressed = process_channel_variable_block(Cr_padded, Q_C, DCT_MAT, BLOCK_SIZE)

# === Crop and convert to RGB ===
Y_final = Y_compressed[:orig_h, :orig_w]
Cb_final = Cb_compressed[:orig_h, :orig_w]
Cr_final = Cr_compressed[:orig_h, :orig_w]
final_rgb = ycbcr_to_rgb(Y_final, Cb_final, Cr_final)

# === Save result ===
out_path = Path(f"outputs/jit_raw_{BLOCK_SIZE}x{BLOCK_SIZE}.jpeg")
plt.imsave(out_path, final_rgb)
