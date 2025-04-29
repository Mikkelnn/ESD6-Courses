```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5737/22H2/2022Update)
Intel Core i7-7820X CPU 3.60GHz (Kaby Lake), 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]     : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL
  DefaultJob : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL


```
| Method                | maxThreads | numWorkers | Mean    | Error    | StdDev   | Median  | Allocated   |
|---------------------- |----------- |----------- |--------:|---------:|---------:|--------:|------------:|
| **Sequential**            | **?**          | **?**          | **4.868 s** | **0.0254 s** | **0.0225 s** | **4.874 s** |   **129.19 MB** |
| **Parallel**              | **4**          | **?**          | **5.180 s** | **0.0419 s** | **0.0349 s** | **5.173 s** |   **165.35 MB** |
| Parallel_V3           | 4          | ?          | 2.681 s | 0.0530 s | 0.0544 s | 2.656 s |   103.36 MB |
| Parallel_V3_NoLock    | 4          | ?          |      NA |       NA |       NA |      NA |          NA |
| Parallel_V3_Partition | 4          | ?          | 2.667 s | 0.0503 s | 0.0471 s | 2.658 s |   103.47 MB |
| **Distributed**           | **?**          | **4**          | **9.450 s** | **0.3001 s** | **0.8850 s** | **9.850 s** | **11429.73 MB** |
| **Parallel**              | **8**          | **?**          | **4.120 s** | **0.0321 s** | **0.0250 s** | **4.122 s** |   **165.35 MB** |
| Parallel_V3           | 8          | ?          | 2.233 s | 0.0406 s | 0.0379 s | 2.231 s |   103.36 MB |
| Parallel_V3_NoLock    | 8          | ?          |      NA |       NA |       NA |      NA |          NA |
| Parallel_V3_Partition | 8          | ?          | 2.205 s | 0.0278 s | 0.0260 s | 2.197 s |   103.52 MB |
| **Distributed**           | **?**          | **8**          | **7.979 s** | **0.1628 s** | **0.4774 s** | **7.919 s** | **12082.39 MB** |
| **Parallel_V3**           | **12**         | **?**          | **2.099 s** | **0.0410 s** | **0.0402 s** | **2.090 s** |   **103.37 MB** |
| Parallel_V3_NoLock    | 12         | ?          |      NA |       NA |       NA |      NA |          NA |
| Parallel_V3_Partition | 12         | ?          | 2.090 s | 0.0275 s | 0.0257 s | 2.091 s |   103.57 MB |
| **Distributed**           | **?**          | **12**         | **8.869 s** | **0.1763 s** | **0.4915 s** | **9.045 s** | **14924.31 MB** |
| **Parallel**              | **16**         | **?**          | **3.939 s** | **0.0272 s** | **0.0254 s** | **3.945 s** |   **165.36 MB** |
| Parallel_V3           | 16         | ?          | 2.002 s | 0.0324 s | 0.0303 s | 1.997 s |   103.39 MB |
| Parallel_V3_NoLock    | 16         | ?          |      NA |       NA |       NA |      NA |          NA |
| Parallel_V3_Partition | 16         | ?          | 2.006 s | 0.0301 s | 0.0281 s | 2.005 s |   103.62 MB |
| **Distributed**           | **?**          | **16**         |      **NA** |       **NA** |       **NA** |      **NA** |          **NA** |

Benchmarks with issues:
  BFSBenchmarks_G2.Parallel_V3_NoLock: DefaultJob [maxThreads=4]
  BFSBenchmarks_G2.Parallel_V3_NoLock: DefaultJob [maxThreads=8]
  BFSBenchmarks_G2.Parallel_V3_NoLock: DefaultJob [maxThreads=12]
  BFSBenchmarks_G2.Parallel_V3_NoLock: DefaultJob [maxThreads=16]
  BFSBenchmarks_G2.Distributed: DefaultJob [numWorkers=16]
