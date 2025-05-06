using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BFSAlgo;

public class Program
{    
    public static void Main(string[] args)
    {
        //GenerateGraphs();
        //Test();
        Benchmark();

        Console.ReadKey();
    }

    static void Benchmark()
    {
        var summary_g1 = BenchmarkRunner.Run<BFSBenchmarks_G1>();
        var summary_g2 = BenchmarkRunner.Run<BFSBenchmarks_G2>();
        var summary_g3 = BenchmarkRunner.Run<BFSBenchmarks_G3>();
    }

    static void Test()
    {
        //GenerateGraphs();
        Console.WriteLine("loading g3..");
        var loadedGraph = GraphService.LoadGraph("g3_adjacency_list.bin");
        Console.WriteLine("Running searcher...");
        Stopwatch sw = Stopwatch.StartNew();
        //Searchers.BFS_Sequential(loadedGraph, 0);
        int timeout = -1; // (int)TimeSpan.FromSeconds(10).TotalMilliseconds

        //var oldMe = GCSettings.LatencyMode;
        //GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        //var visited = GraphSearchers.BFS_ParallelSharedMemory(loadedGraph, 0, 12);
        var visited = GraphSearchers.BFS_Distributed(loadedGraph, 0, numWorkers: 12, timeout);
        //Searchers.BFS_Parallel_V3(loadedGraph, 0, 12);
        //Searchers.BFS_Parallel_V3_Partition(loadedGraph, 0, 4);
        sw.Stop();
        Console.WriteLine($"Finished in: {sw.ElapsedMilliseconds} ms, all visited: {visited.IsAllSet()}");
        //GCSettings.LatencyMode = oldMe;
    }

    static void GenerateGraphs()
    {
        uint G1_NodeCount = 100_000;
        uint G1_MaxEdgesPerNode = 671;

        uint G2_NodeCount = 10_000_000;
        uint G2_MaxEdgesPerNode = 67;

        uint G3_NodeCount = 10_000_000;
        uint G3_MaxEdgesPerNode = 670;

        string G1_FileName = "g1_adjacency_list.bin";
        string G2_FileName = "g2_adjacency_list.bin";
        string G3_FileName = "g3_adjacency_list.bin";

        List<uint>[] g;

        Console.WriteLine("Generating graph G1...");
        g = GraphService.GenerateGraph(G1_NodeCount, G1_MaxEdgesPerNode);
        Console.WriteLine("Saving graph G1 to file...");
        GraphService.SaveGraph(g, G1_FileName);

        Console.WriteLine("Generating graph G2...");
        g = GraphService.GenerateGraph(G2_NodeCount, G2_MaxEdgesPerNode);
        Console.WriteLine("Saving graph G2 to file...");
        GraphService.SaveGraph(g, G2_FileName);

        Console.WriteLine("Generating graph G3...");
        GraphService.GenerateGraphToDisk(G3_FileName, G3_NodeCount, G3_MaxEdgesPerNode);
    }
}

[MemoryDiagnoser(false)]
[MarkdownExporter, HtmlExporter, CsvExporter, RPlotExporter]
public class BFSBenchmarks_G1
{
    public static List<uint>[] loadedGraph;

    [GlobalSetup]
    public void Setup()
    {
        string FileName = "g1_adjacency_list.bin";
        Console.WriteLine($"Loading graph {FileName} from file...");
        loadedGraph = GraphService.LoadGraph(FileName);
    }

    [GlobalCleanup]
    public void Clenup()
    {
        
    }

    [Benchmark(Baseline = true)]
    public void Sequential() => GraphSearchers.BFS_Sequential(loadedGraph, 0);

    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void BFS_ParallelSharedMemory(int maxThreads) => GraphSearchers.BFS_ParallelSharedMemory(loadedGraph, 0, maxThreads);
        
    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void Distributed(int numWorkers) => GraphSearchers.BFS_Distributed(loadedGraph, 0, numWorkers);
}

[MemoryDiagnoser(false)]
[MarkdownExporter, HtmlExporter, CsvExporter, RPlotExporter]
public class BFSBenchmarks_G2
{
    public static List<uint>[] loadedGraph;

    [GlobalSetup]
    public void Setup()
    {
        string FileName = "g2_adjacency_list.bin";
        Console.WriteLine($"Loading graph {FileName} from file...");
        loadedGraph = GraphService.LoadGraph(FileName);
    }

    [Benchmark(Baseline = true)]
    public void Sequential() => GraphSearchers.BFS_Sequential(loadedGraph, 0);

    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void BFS_ParallelSharedMemory(int maxThreads) => GraphSearchers.BFS_ParallelSharedMemory(loadedGraph, 0, maxThreads);

    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void Distributed(int numWorkers) => GraphSearchers.BFS_Distributed(loadedGraph, 0, numWorkers);
}

[MemoryDiagnoser(false)]
[MarkdownExporter, HtmlExporter, CsvExporter, RPlotExporter]
public class BFSBenchmarks_G3
{
    public static List<uint>[] loadedGraph;

    [GlobalSetup]
    public void Setup()
    {
        string FileName = "g3_adjacency_list.bin";
        Console.WriteLine($"Loading graph {FileName} from file...");
        loadedGraph = GraphService.LoadGraph(FileName);
    }

    [Benchmark(Baseline = true)]
    public void Sequential() => GraphSearchers.BFS_Sequential(loadedGraph, 0);

    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void BFS_ParallelSharedMemory(int maxThreads) => GraphSearchers.BFS_ParallelSharedMemory(loadedGraph, 0, maxThreads);

    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void Distributed(int numWorkers) => GraphSearchers.BFS_Distributed(loadedGraph, 0, numWorkers);
}


