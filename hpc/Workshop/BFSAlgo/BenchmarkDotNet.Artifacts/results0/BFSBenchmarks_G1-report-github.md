```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5737/22H2/2022Update)
Intel Core i7-7820X CPU 3.60GHz (Kaby Lake), 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]     : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL
  DefaultJob : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL


```
| Method                | maxThreads | numWorkers | Mean      | Error     | StdDev    | Allocated  |
|---------------------- |----------- |----------- |----------:|----------:|----------:|-----------:|
| **Sequential**            | **?**          | **?**          | **105.40 ms** |  **1.499 ms** |  **1.252 ms** |    **1.01 MB** |
| **Parallel**              | **4**          | **?**          | **364.51 ms** |  **0.714 ms** |  **0.668 ms** |    **1.82 MB** |
| Parallel_V3           | 4          | ?          |  40.75 ms |  0.297 ms |  0.278 ms |     1.4 MB |
| Parallel_V3_NoLock    | 4          | ?          |        NA |        NA |        NA |         NA |
| Parallel_V3_Partition | 4          | ?          |  41.26 ms |  0.144 ms |  0.134 ms |    1.47 MB |
| **Distributed**           | **?**          | **4**          | **928.11 ms** | **16.743 ms** | **15.662 ms** | **1046.26 MB** |
| **Parallel**              | **8**          | **?**          | **217.96 ms** |  **1.872 ms** |  **1.751 ms** |    **1.82 MB** |
| Parallel_V3           | 8          | ?          |  28.67 ms |  0.108 ms |  0.101 ms |     1.4 MB |
| Parallel_V3_NoLock    | 8          | ?          |        NA |        NA |        NA |         NA |
| Parallel_V3_Partition | 8          | ?          |  28.99 ms |  0.489 ms |  0.502 ms |     1.5 MB |
| **Distributed**           | **?**          | **8**          | **883.43 ms** | **16.584 ms** | **16.288 ms** | **1049.94 MB** |
| **Parallel_V3**           | **12**         | **?**          |  **27.13 ms** |  **0.429 ms** |  **0.401 ms** |    **1.41 MB** |
| Parallel_V3_NoLock    | 12         | ?          |        NA |        NA |        NA |         NA |
| Parallel_V3_Partition | 12         | ?          |  26.98 ms |  0.118 ms |  0.110 ms |    1.53 MB |
| **Distributed**           | **?**          | **12**         | **821.51 ms** | **16.422 ms** | **30.029 ms** |  **920.43 MB** |
| **Parallel**              | **16**         | **?**          | **158.50 ms** |  **0.438 ms** |  **0.410 ms** |    **1.83 MB** |
| Parallel_V3           | 16         | ?          |  25.39 ms |  0.047 ms |  0.040 ms |    1.41 MB |
| Parallel_V3_NoLock    | 16         | ?          |        NA |        NA |        NA |         NA |
| Parallel_V3_Partition | 16         | ?          |  25.84 ms |  0.150 ms |  0.141 ms |    1.56 MB |
| **Distributed**           | **?**          | **16**         |        **NA** |        **NA** |        **NA** |         **NA** |

Benchmarks with issues:
  BFSBenchmarks_G1.Parallel_V3_NoLock: DefaultJob [maxThreads=4]
  BFSBenchmarks_G1.Parallel_V3_NoLock: DefaultJob [maxThreads=8]
  BFSBenchmarks_G1.Parallel_V3_NoLock: DefaultJob [maxThreads=12]
  BFSBenchmarks_G1.Parallel_V3_NoLock: DefaultJob [maxThreads=16]
  BFSBenchmarks_G1.Distributed: DefaultJob [numWorkers=16]
