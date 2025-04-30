import subprocess

# Path to hyperfine executable (adjust if needed)
HYPERFINE = "hyperfine.exe"  # or just "hyperfine" if it's in your PATH

# Scripts to benchmark
SCRIPTS = [
    ("NumPy vectorized (PNG)", "python scripts/numpy_vectorized_png.py"),
    ("NumPy vectorized (NEF)", "python scripts/numpy_vectorized_raw.py"),
    ("JIT Numba (PNG)", "python scripts/jit_png.py"),
    ("JIT Numba (NEF)", "python scripts/jit_raw.py"),
    ("MPI (PNG)", "mpiexec -n 4 python scripts/mpi_png.py"),
    ("MPI (NEF)", "mpiexec -n 4 python scripts/mpi_raw.py"),
    ("GPU (PNG)", "python scripts/opencl_png.py"),
    ("GPU (NEF)", "python scripts/opencl_raw.py"),
]

RUNS = 10  # Number of repetitions

def run_benchmark(label, command):
    print(f"\n⏱️ Benchmarking: {label}")
    result = subprocess.run([
        HYPERFINE,
        "--runs", str(RUNS),
        "--warmup", "1",
        #"--style", "basic",
        "--show-output",  # 🔍 Show error output from Python script
        command
    ])
    if result.returncode != 0:
        print(f"❌ Error running benchmark for {label}")

if __name__ == "__main__":
    for label, cmd in SCRIPTS:
        run_benchmark(label, cmd)
