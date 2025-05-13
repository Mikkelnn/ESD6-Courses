import numpy as np
import rawpy
from matplotlib import pyplot as plt

# Paso 1: Cargar imagen RAW y convertir a escala de grises
with rawpy.imread('image.NEF') as raw:
    rgb = raw.postprocess()

gray = 0.299 * rgb[:, :, 0] + 0.587 * rgb[:, :, 1] + 0.114 * rgb[:, :, 2]
gray = gray.astype(np.float32)
gray -= 128  # centrar en 0

# Paso 2: Padding a múltiplos de 8
def pad_image(image, block_size=8):
    h, w = image.shape
    pad_h = (block_size - h % block_size) % block_size
    pad_w = (block_size - w % block_size) % block_size
    padded = np.pad(image, ((0, pad_h), (0, pad_w)), mode='constant')
    return padded, h, w

gray_padded, orig_h, orig_w = pad_image(gray, block_size=8)

# Paso 3: DCT 2D manual (tipo II)
def dct_2d_manual(block):
    N = block.shape[0]
    result = np.zeros((N, N))
    for u in range(N):
        for v in range(N):
            sum_val = 0
            for x in range(N):
                for y in range(N):
                    sum_val += block[x, y] * \
                               np.cos(np.pi * (2*x + 1) * u / (2 * N)) * \
                               np.cos(np.pi * (2*y + 1) * v / (2 * N))
            alpha_u = np.sqrt(1/N) if u == 0 else np.sqrt(2/N)
            alpha_v = np.sqrt(1/N) if v == 0 else np.sqrt(2/N)
            result[u, v] = alpha_u * alpha_v * sum_val
    return result

# Paso 5: IDCT 2D manual (tipo III)
def idct_2d_manual(block):
    N = block.shape[0]
    result = np.zeros((N, N))
    for x in range(N):
        for y in range(N):
            sum_val = 0
            for u in range(N):
                for v in range(N):
                    alpha_u = np.sqrt(1/N) if u == 0 else np.sqrt(2/N)
                    alpha_v = np.sqrt(1/N) if v == 0 else np.sqrt(2/N)
                    sum_val += alpha_u * alpha_v * block[u, v] * \
                               np.cos(np.pi * (2*x + 1) * u / (2 * N)) * \
                               np.cos(np.pi * (2*y + 1) * v / (2 * N))
            result[x, y] = sum_val
    return result

# Paso 4: Matriz de cuantización JPEG estándar
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

# Procesamiento por bloques
block_size = 8
h, w = gray_padded.shape
compressed = np.zeros_like(gray_padded)
reconstructed = np.zeros_like(gray_padded)

for i in range(0, h, block_size):
    for j in range(0, w, block_size):
        block = gray_padded[i:i+block_size, j:j+block_size]
        dct_block = dct_2d_manual(block)
        quantized = np.round(dct_block / Q)
        dequantized = quantized * Q
        idct_block = idct_2d_manual(dequantized)
        reconstructed[i:i+block_size, j:j+block_size] = idct_block

# Reversión del centrado y recorte a tamaño original
final_image = np.clip(reconstructed + 128, 0, 255).astype(np.uint8)
final_image = final_image[:orig_h, :orig_w]

# Mostrar y guardar
plt.imsave('reconstruida.jpg', final_image, cmap='gray')
plt.imshow(final_image, cmap='gray')
plt.title("Reconstruida")
plt.axis('off')
plt.show()
