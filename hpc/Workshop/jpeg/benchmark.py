import time
import subprocess
import numpy as np

# Lista de implementaciones
implementaciones = [
    
     ("JIT Numba (NEF)", "python scripts/jit_raw.py"), 
    ("JIT Numba (PNG)", "python scripts/jit_png.py"), 
    ("Numpy Vectorized (NEF)", "python scripts/numpy_vectorized_raw.py"), 
    ("Numpy Vectorized (PNG)", "python scripts/numpy_vectorized_png.py"), 
    ("OpenCL (NEF)", "python scripts/opencl_raw.py"), 
    ("OpenCL (PNG)", "python scripts/opencl_png.py"),
    ("MPI (NEF)", "mpiexec -n 1 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 1 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 2 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 2 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 3 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 3 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 4 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 4 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 5 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 5 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 6 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 6 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 7 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 7 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 8 python scripts/mpi_raw.py"),
    ("MPI (PNG)", "mpiexec -n 8 python scripts/mpi_png.py"),
]

def run_command(cmd):
    start = time.perf_counter()
    try:
        subprocess.run(cmd, shell=True, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    except subprocess.CalledProcessError as e:
        print(f"❌ Error en: {cmd}\n{e.stderr.decode()}")
        return None
    end = time.perf_counter()
    return end - start

def benchmark_command(name, cmd, reps=5):
    tiempos = []
    print(f"\n🏁 Benchmarking: {name}")
    for i in range(reps):
        print(f"  → Ejecución {i+1}/{reps}...", end="")
        t = run_command(cmd)
        if t is not None:
            tiempos.append(t)
            print(f" {t:.4f} s")
        else:
            print(" Error")
    tiempos = np.array(tiempos)
    return {
        'media': np.mean(tiempos),
        'desviación estándar': np.std(tiempos),
        'varianza': np.var(tiempos),
        'repeticiones': reps,
        'todas las repeticiones': tiempos.tolist()
    }

def main():
    reps = 5  # número de repeticiones por implementación
    resultados = {}

    for nombre, comando in implementaciones:
        resultados[nombre] = benchmark_command(nombre, comando, reps)

    print("\n📊 RESULTADOS FINALES:")
    for nombre, stats in resultados.items():
        print(f"\n[{nombre}]")
        for clave, valor in stats.items():
            if isinstance(valor, float):
                print(f"  {clave}: {valor:.6f} s")
            elif isinstance(valor, list):
                print(f"  {clave}: {[round(v, 4) for v in valor]}")
            else:
                print(f"  {clave}: {valor}")

if __name__ == "__main__":
    main()
