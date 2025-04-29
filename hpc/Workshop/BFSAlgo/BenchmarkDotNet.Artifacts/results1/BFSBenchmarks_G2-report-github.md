```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5737/22H2/2022Update)
Intel Core i7-7820X CPU 3.60GHz (Kaby Lake), 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]     : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL [AttachedDebugger]
  DefaultJob : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL


```
| Method      | numWorkers | Mean    | Error    | StdDev   | Median  | Allocated |
|------------ |----------- |--------:|---------:|---------:|--------:|----------:|
| **Distributed** | **8**          | **5.076 s** | **0.1824 s** | **0.5378 s** | **4.895 s** |   **4.17 GB** |
| **Distributed** | **12**         | **5.283 s** | **0.1369 s** | **0.4038 s** | **5.418 s** |   **4.92 GB** |
