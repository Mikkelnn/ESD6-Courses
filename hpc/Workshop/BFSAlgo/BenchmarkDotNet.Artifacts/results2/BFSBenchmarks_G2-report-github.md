```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.5737/22H2/2022Update)
Intel Core i7-7820X CPU 3.60GHz (Kaby Lake), 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.203
  [Host]     : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL
  DefaultJob : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL


```
| Method      | numWorkers | Mean    | Error    | StdDev   | Allocated |
|------------ |----------- |--------:|---------:|---------:|----------:|
| **Distributed** | **8**          | **2.900 s** | **0.0554 s** | **0.0681 s** |    **4.6 GB** |
| **Distributed** | **12**         | **2.916 s** | **0.0458 s** | **0.0383 s** |   **5.65 GB** |
