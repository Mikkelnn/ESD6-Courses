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

# ---------- Step 2: Pad image to a multiple of block size ----------
def pad_to_block_multiple(image, block_h, block_w):
    h, w = image.shape
    pad_h = (block_h - (h % block_h)) % block_h
    pad_w = (block_w - (w % block_w)) % block_w
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

# Choose any block size here:
block_h, block_w = 64, 64

Y_padded, orig_h, orig_w = pad_to_block_multiple(Y, block_h, block_w)
Cb_padded, _, _ = pad_to_block_multiple(Cb, block_h, block_w)
Cr_padded, _, _ = pad_to_block_multiple(Cr, block_h, block_w)

# ---------- Step 3: DCT/IDCT matrices ----------
def dct_matrix(N):
    C = np.zeros((N, N))
    for k in range(N):
        for n in range(N):
            alpha = np.sqrt(1/N) if k == 0 else np.sqrt(2/N)
            C[k, n] = alpha * np.cos(np.pi * (2*n + 1) * k / (2 * N))
    return C

C_h = dct_matrix(Y_padded.shape[0])
C_w = dct_matrix(Y_padded.shape[1])

# ---------- Step 4: Quantization matrix ----------
Q_luma_8x8 = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
], dtype=np.float32)

def scale_quant_matrix(Q, quality):
    quality = max(1, min(100, quality))
    if quality < 50:
        scale = 5000 / quality
    else:
        scale = 200 - quality * 2
    return np.clip((Q * scale + 50) / 100, 1, 255).astype(np.float32)

def upscale_quant_matrix(Q_base, shape):
    reps = (shape[0] // Q_base.shape[0], shape[1] // Q_base.shape[1])
    return np.tile(Q_base, reps)

# ---------- Step 5: DCT compression function ----------
def process_channel_full_block(channel, Q_base, quality=100):
    block = channel - 128
    dct_block = C_h @ block @ C_w.T

    Q_scaled = scale_quant_matrix(Q_base, quality)
    Q_big = upscale_quant_matrix(Q_scaled, block.shape)

    quantized = np.round(dct_block / Q_big)
    dequantized = quantized * Q_big

    return C_h.T @ dequantized @ C_w + 128

# ---------- Step 6: Compress all channels ----------
quality = 100 # Change this to control compression

Y_final = process_channel_full_block(Y_padded, Q_luma_8x8, quality)
Cb_final = process_channel_full_block(Cb_padded, Q_luma_8x8, quality)
Cr_final = process_channel_full_block(Cr_padded, Q_luma_8x8, quality)

# ---------- Step 7: Crop to original size ----------
Y_final = Y_final[:orig_h, :orig_w]
Cb_final = Cb_final[:orig_h, :orig_w]
Cr_final = Cr_final[:orig_h, :orig_w]

# ---------- Step 8: Convert back to RGB ----------
final_rgb = ycbcr_to_rgb(Y_final, Cb_final, Cr_final)

# ---------- Step 9: Save the image ----------
out_path = Path(f"outputs/output_numpy_png_{block_h}x{block_w}.jpeg")
plt.imsave(out_path, final_rgb.astype(np.uint8))
