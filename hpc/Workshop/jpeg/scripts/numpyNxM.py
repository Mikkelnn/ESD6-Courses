import numpy as np
from matplotlib import pyplot as plt
import imageio
from pathlib import Path

# ---------- Step 1: Load image and convert to YCbCr ----------
img_path = Path("images/gato.png")
rgb = imageio.imread(img_path).astype(np.float32)

# RGB to YCbCr conversion
def rgb_to_ycbcr(image):
    R = image[:, :, 0]
    G = image[:, :, 1]
    B = image[:, :, 2]
    Y  =  0.299 * R + 0.587 * G + 0.114 * B
    Cb = -0.168736 * R - 0.331264 * G + 0.5 * B + 128
    Cr =  0.5 * R - 0.418688 * G - 0.081312 * B + 128
    return Y, Cb, Cr

# YCbCr to RGB conversion
def ycbcr_to_rgb(Y, Cb, Cr):
    R = Y + 1.402 * (Cr - 128)
    G = Y - 0.344136 * (Cb - 128) - 0.714136 * (Cr - 128)
    B = Y + 1.772 * (Cb - 128)
    rgb = np.stack((R, G, B), axis=-1)
    return np.clip(rgb, 0, 255).astype(np.uint8)

Y, Cb, Cr = rgb_to_ycbcr(rgb)

# ---------- Step 2: Pad image to make sure it's square (N x N) ----------
def pad_to_square(image):
    h, w = image.shape
    size = max(h, w)
    padded = np.pad(image, ((0, size - h), (0, size - w)), mode='constant')
    return padded, h, w

Y_padded, orig_h, orig_w = pad_to_square(Y)
Cb_padded, _, _ = pad_to_square(Cb)
Cr_padded, _, _ = pad_to_square(Cr)

# ---------- Step 3: DCT/IDCT matrices ----------
def dct_matrix(N):
    C = np.zeros((N, N))
    for k in range(N):
        for n in range(N):
            alpha = np.sqrt(1/N) if k == 0 else np.sqrt(2/N)
            C[k, n] = alpha * np.cos(np.pi * (2*n + 1) * k / (2 * N))
    return C

# Use full image size as block
block_size = Y_padded.shape[0]  # assuming square padded image
C = dct_matrix(block_size)

# ---------- Step 4: DCT and IDCT with full image block ----------
def dct_2d(block):
    return C @ block @ C.T

def idct_2d(block):
    return C.T @ block @ C

# ---------- Step 5: Disable quantization (for now) ----------
def process_channel_full_block(channel):
    block = channel - 128
    dct_block = dct_2d(block)
    quantized = np.round(dct_block)  # Or divide by 1 if desired
    dequantized = quantized  # No quantization matrix
    return idct_2d(dequantized) + 128

Y_final = process_channel_full_block(Y_padded)
Cb_final = process_channel_full_block(Cb_padded)
Cr_final = process_channel_full_block(Cr_padded)

# ---------- Step 6: Crop to original size ----------
Y_final = Y_final[:orig_h, :orig_w]
Cb_final = Cb_final[:orig_h, :orig_w]
Cr_final = Cr_final[:orig_h, :orig_w]

# ---------- Step 7: Convert back to RGB ----------
final_rgb = ycbcr_to_rgb(Y_final, Cb_final, Cr_final)

# ---------- Step 8: Save the image ----------
out_path = Path("outputs/output_numpy_fullblock.jpeg")
plt.imsave(out_path, final_rgb.astype(np.uint8))
