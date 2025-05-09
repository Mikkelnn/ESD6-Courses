import numpy as np
import rawpy
from matplotlib import pyplot as plt

# Standard JPEG Luminance Quantization Matrix (8x8)
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

def pad_image_to_block_size(image, block_size=8):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    return np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant', constant_values=0)

def dct_1d(vector):
    N = len(vector)
    result = np.zeros(N)
    for k in range(N):
        sum_val = 0
        for n in range(N):
            sum_val += vector[n] * np.cos(np.pi * k * (2*n + 1) / (2 * N))
        result[k] = sum_val * 2
    return result

def dct_2d(block):
    N = block.shape[0]
    temp = np.zeros((N, N))
    result = np.zeros((N, N))
    for i in range(N):
        temp[i, :] = dct_1d(block[i, :])
    for j in range(N):
        result[:, j] = dct_1d(temp[:, j])
    return result

def idct_1d(vector):
    N = len(vector)
    result = np.zeros(N)
    for n in range(N):
        sum_val = 0
        for k in range(N):
            sum_val += vector[k] * np.cos(np.pi * k * (2*n + 1) / (2 * N))
        result[n] = sum_val * 2 / N
    return result

def idct_2d(block):
    N = block.shape[0]
    temp = np.zeros((N, N))
    result = np.zeros((N, N))
    for j in range(N):
        temp[:, j] = idct_1d(block[:, j])
    for i in range(N):
        result[i, :] = idct_1d(temp[i, :])
    return result

def jpeg_compress(image, block_size=8):
    h, w = image.shape
    compressed = np.zeros_like(image, dtype=np.float32)
    for i in range(0, h, block_size):
        for j in range(0, w, block_size):
            block = image[i:i+block_size, j:j+block_size] - 128
            dct_block = dct_2d(block)
            quantized = np.round(dct_block / Q)
            compressed[i:i+block_size, j:j+block_size] = quantized
    return compressed

def jpeg_decompress(compressed, block_size=8):
    h, w = compressed.shape
    reconstructed = np.zeros_like(compressed, dtype=np.float32)
    for i in range(0, h, block_size):
        for j in range(0, w, block_size):
            quantized = compressed[i:i+block_size, j:j+block_size]
            dequantized = quantized * Q
            idct_block = idct_2d(dequantized) + 128
            reconstructed[i:i+block_size, j:j+block_size] = np.clip(idct_block, 0, 255)
    return reconstructed.astype(np.uint8)

# Cargar imagen RAW (.NEF)
with rawpy.imread('image.NEF') as raw:
    rgb = raw.postprocess()

# Convertir a escala de grises
gray = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]
gray = gray.astype(np.float32)
original_shape = gray.shape

# Rellenar para bloques de 8x8
gray_padded = pad_image_to_block_size(gray)

# Compresión y descompresión
compressed = jpeg_compress(gray_padded)
reconstructed = jpeg_decompress(compressed)

# Recortar a tamaño original
reconstructed = reconstructed[:original_shape[0], :original_shape[1]]

# Mostrar imágenes
plt.subplot(1, 2, 1)
plt.imshow(gray, cmap='gray')
plt.title('Original')

plt.subplot(1, 2, 2)
plt.imshow(reconstructed, cmap='gray')
plt.title('Reconstruida')

plt.show()

# Guardar la imagen reconstruida como JPEG
plt.imsave('reconstruida.jpg', reconstructed, cmap='gray')
