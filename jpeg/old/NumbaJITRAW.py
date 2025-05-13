import numpy as np
from matplotlib import pyplot as plt
import imageio
from numba import jit
import rawpy  # Library to read raw images

# ---------- Load image and convert to YCbCr ----------
def load_raw_image(filename):
    # Open the NEF file with rawpy
    raw = rawpy.imread(filename)
    # Convert to RGB
    rgb = raw.postprocess()
    return rgb.astype(np.float32)
rgb = load_raw_image('image.nef')

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

# Step 2: Padding to multiples of 8
def pad_image(image, block_size=8):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

Y_padded, orig_h, orig_w = pad_image(Y)
Cb_padded, _, _ = pad_image(Cb)
Cr_padded, _, _ = pad_image(Cr)

# Step 3: Define DCT and IDCT using Numba JIT
@jit(nopython=True)
def dct_2d_numba(block):
    N = block.shape[0]
    result = np.zeros((N, N))
    for u in range(N):
        for v in range(N):
            sum_val = 0.0
            for x in range(N):
                for y in range(N):
                    sum_val += block[x, y] * np.cos(np.pi * (2*x + 1) * u / (2 * N)) * np.cos(np.pi * (2*y + 1) * v / (2 * N))
            alpha_u = np.sqrt(1/N) if u == 0 else np.sqrt(2/N)
            alpha_v = np.sqrt(1/N) if v == 0 else np.sqrt(2/N)
            result[u, v] = alpha_u * alpha_v * sum_val
    return result

@jit(nopython=True)
def idct_2d_numba(block):
    N = block.shape[0]
    result = np.zeros((N, N))
    for x in range(N):
        for y in range(N):
            sum_val = 0.0
            for u in range(N):
                for v in range(N):
                    alpha_u = np.sqrt(1/N) if u == 0 else np.sqrt(2/N)
                    alpha_v = np.sqrt(1/N) if v == 0 else np.sqrt(2/N)
                    sum_val += alpha_u * alpha_v * block[u, v] * np.cos(np.pi * (2*x + 1) * u / (2 * N)) * np.cos(np.pi * (2*y + 1) * v / (2 * N))
            result[x, y] = sum_val
    return result

# Step 4: Standard JPEG Quantization Matrices
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

# Step 5: Process each channel
def process_channel_numba(channel, Q):
    block_size = 8
    h, w = channel.shape
    compressed = np.zeros_like(channel)
    for i in range(0, h, block_size):
        for j in range(0, w, block_size):
            block = channel[i:i+block_size, j:j+block_size] - 128
            dct_block = dct_2d_numba(block)
            quantized = np.round(dct_block / Q)
            dequantized = quantized * Q
            idct_block = idct_2d_numba(dequantized) + 128
            compressed[i:i+block_size, j:j+block_size] = idct_block
    return compressed

Y_compressed = process_channel_numba(Y_padded, Q_Y)
Cb_compressed = process_channel_numba(Cb_padded, Q_C)
Cr_compressed = process_channel_numba(Cr_padded, Q_C)

# Step 6: Crop back to original size
Y_final = Y_compressed[:orig_h, :orig_w]
Cb_final = Cb_compressed[:orig_h, :orig_w]
Cr_final = Cr_compressed[:orig_h, :orig_w]

# Step 7: Convert back to RGB
final_rgb = ycbcr_to_rgb(Y_final, Cb_final, Cr_final)

# Step 8: Save final image
plt.imsave('gatorapidocolor_numba.jpg', final_rgb)
# plt.imshow(final_rgb)
# plt.title("Reconstructed in Color with Numba")
# plt.axis('off')
# plt.show()
