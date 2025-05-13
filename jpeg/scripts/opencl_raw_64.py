import numpy as np
import pyopencl as cl
import imageio.v2 as imageio
from PIL import Image
from pathlib import Path
import matplotlib.pyplot as plt
import rawpy  # Library to read raw images


# ---------- Parámetros ----------
block_size = 64
scale = 0.5  # Escala para las tablas de cuantización (0.25 = más calidad, 0.75 = menos calidad)

# ---------- Tablas de cuantización escaladas ----------
QY_base = np.array([
    [16,11,10,16,24,40,51,61],
    [12,12,14,19,26,58,60,55],
    [14,13,16,24,40,57,69,56],
    [14,17,22,29,51,87,80,62],
    [18,22,37,56,68,109,103,77],
    [24,35,55,64,81,104,113,92],
    [49,64,78,87,103,121,120,101],
    [72,92,95,98,112,100,103,99]
], dtype=np.float32)

QC_base = np.array([
    [17,18,24,47,99,99,99,99],
    [18,21,26,66,99,99,99,99],
    [24,26,56,99,99,99,99,99],
    [47,66,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99],
    [99,99,99,99,99,99,99,99]
], dtype=np.float32)

QY = QY_base * scale
QC = QC_base * scale

# ---------- Cargar imagen y convertir a YCbCr ----------
def load_raw_image(filename):
    img_path = Path("images") / filename
    if not img_path.exists():
        raise FileNotFoundError(f"❌ RAW image not found: {img_path}")
    raw = rawpy.imread(str(img_path))  # rawpy requires a string path
    rgb = raw.postprocess()
    return rgb.astype(np.float32)

# Usage:
rgb = load_raw_image("image.nef")

R, G, B = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
Y  =  0.299 * R + 0.587 * G + 0.114 * B
Cb = -0.168736 * R - 0.331264 * G + 0.5 * B + 128
Cr =  0.5 * R - 0.418688 * G - 0.081312 * B + 128

height, width = Y.shape
height -= height % block_size
width -= width % block_size

Y, Cb, Cr = Y[:height, :width], Cb[:height, :width], Cr[:height, :width]

# ---------- OpenCL ----------
ctx = cl.create_some_context()
queue = cl.CommandQueue(ctx)

program_src = """
__constant float PI = 3.14159265358979;

__kernel void dct_quant_block(__global float* input, __global float* output,
                               __constant float* Q, int width, int height) {
    int x0 = get_global_id(0) * 8;
    int y0 = get_global_id(1) * 8;

    float block[8][8];
    float tmp[8][8];

    for (int i=0; i<8; ++i)
        for (int j=0; j<8; ++j)
            block[i][j] = input[(y0+i)*width + (x0+j)] - 128.0f;

    for (int i=0; i<8; ++i)
        for (int u=0; u<8; ++u) {
            float sum = 0.0f;
            for (int x=0; x<8; ++x)
                sum += block[i][x] * cos((2.0f*x+1.0f)*u*PI/16.0f);
            float Cu = (u==0) ? 0.7071f : 1.0f;
            tmp[i][u] = 0.5f * Cu * sum;
        }

    for (int u=0; u<8; ++u)
        for (int v=0; v<8; ++v) {
            float sum = 0.0f;
            for (int y=0; y<8; ++y)
                sum += tmp[y][v] * cos((2.0f*y+1.0f)*u*PI/16.0f);
            float Cu = (u==0) ? 0.7071f : 1.0f;
            float dct = 0.5f * Cu * sum;
            output[(y0+u)*width + (x0+v)] = round(dct / Q[u*8 + v]);
        }
}

__kernel void idct_dequant_block(__global float* input, __global float* output,
                                  __constant float* Q, int width, int height) {
    int x0 = get_global_id(0) * 8;
    int y0 = get_global_id(1) * 8;

    float block[8][8];
    float tmp[8][8];

    for (int i=0; i<8; ++i)
        for (int j=0; j<8; ++j)
            block[i][j] = input[(y0+i)*width + (x0+j)] * Q[i*8 + j];

    for (int i=0; i<8; ++i)
        for (int x=0; x<8; ++x) {
            float sum = 0.0f;
            for (int u=0; u<8; ++u) {
                float Cu = (u==0) ? 0.7071f : 1.0f;
                sum += Cu * block[u][i] * cos((2.0f*x+1.0f)*u*PI/16.0f);
            }
            tmp[x][i] = 0.5f * sum;
        }

    for (int y=0; y<8; ++y)
        for (int x=0; x<8; ++x) {
            float sum = 0.0f;
            for (int v=0; v<8; ++v) {
                float Cv = (v==0) ? 0.7071f : 1.0f;
                sum += Cv * tmp[y][v] * cos((2.0f*x+1.0f)*v*PI/16.0f);
            }
            float val = 0.5f * sum + 128.0f;
            output[(y0+y)*width + (x0+x)] = clamp(val, 0.0f, 255.0f);
        }
}
"""

program = cl.Program(ctx, program_src).build()

def process_channel(channel, Q):
    mf = cl.mem_flags
    flat = channel.flatten().astype(np.float32)
    buf_in = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=flat)
    buf_out = cl.Buffer(ctx, mf.READ_WRITE, flat.nbytes)
    Q_buf = cl.Buffer(ctx, mf.READ_ONLY | mf.COPY_HOST_PTR, hostbuf=Q.flatten())

    program.dct_quant_block(queue, (width//8, height//8), None, buf_in, buf_out, Q_buf, np.int32(width), np.int32(height))
    program.idct_dequant_block(queue, (width//8, height//8), None, buf_out, buf_in, Q_buf, np.int32(width), np.int32(height))

    result = np.empty_like(flat)
    cl.enqueue_copy(queue, result, buf_in)
    return result.reshape((height, width))

# ---------- Procesar canales ----------
Y_out  = process_channel(Y, QY)
Cb_out = process_channel(Cb, QC)
Cr_out = process_channel(Cr, QC)

# ---------- Convertir YCbCr -> RGB ----------
Y_out = np.clip(Y_out, 0, 255)
Cb_out = np.clip(Cb_out, 0, 255)
Cr_out = np.clip(Cr_out, 0, 255)

R = Y_out + 1.402 * (Cr_out - 128)
G = Y_out - 0.344136 * (Cb_out - 128) - 0.714136 * (Cr_out - 128)
B = Y_out + 1.772 * (Cb_out - 128)

rgb_out = np.stack([R, G, B], axis=-1)
rgb_out = np.clip(rgb_out, 0, 255).astype(np.uint8)

# ---------- Guardar imagen ----------
output_path = Path("outputs/opencl_raw_64.jpeg")
output_path.parent.mkdir(parents=True, exist_ok=True)
Image.fromarray(rgb_out).save(output_path, quality=100)

print(f"Imagen guardada en: {output_path}")

