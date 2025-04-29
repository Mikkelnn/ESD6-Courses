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
    public static async Task Main(string[] args)
    {
        //GenerateGraphs();
        //Console.WriteLine("loading g2..");
        //var loadedGraph = GraphService.LoadGraph("g2_adjacency_list.bin");
        //Console.WriteLine("Running searcher...");
        //Stopwatch sw = Stopwatch.StartNew();
        //Searchers.BFS_Sequential(loadedGraph, 0);
        //int timeout = -1; // (int)TimeSpan.FromSeconds(10).TotalMilliseconds

        //var oldMe = GCSettings.LatencyMode;
        //GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        //var visited = Searchers.BFS_Distributed(loadedGraph, 0, numWorkers: 12, timeout);
        //Searchers.BFS_Parallel_V3(loadedGraph, 0, 12);
        //Searchers.BFS_Parallel_V3_Partition(loadedGraph, 0, 4);
        //sw.Stop();
        //Console.WriteLine($"Finished in: {sw.ElapsedMilliseconds} ms, all visited: {visited.IsAllSet()}");
        //GCSettings.LatencyMode = oldMe;

        //var summary_g1 = BenchmarkRunner.Run<BFSBenchmarks_G1>();
        var summary_g2 = BenchmarkRunner.Run<BFSBenchmarks_G2>();

        Console.ReadKey();
    }

    void GenerateGraphs()
    {
        uint G1_NodeCount = 100_000;
        uint G1_TargetTotalEdges = 67_108_864;

        uint G2_NodeCount = 10_000_000;
        uint G2_TargetTotalEdges = 671_088_640;

        string G1_FileName = "g1_adjacency_list.bin";
        string G2_FileName = "g2_adjacency_list.bin";

        List<uint>[] g;

        Console.WriteLine("Generating graph G1...");
        g = GraphService.GenerateGraph(G1_NodeCount, maxEdgesPerNode: G1_TargetTotalEdges / G1_NodeCount);
        Console.WriteLine("Saving graph G1 to file...");
        GraphService.SaveGraph(g, G1_FileName);

        Console.WriteLine("Generating graph G2...");
        g = GraphService.GenerateGraph(G2_NodeCount, maxEdgesPerNode: G2_TargetTotalEdges / G2_NodeCount);
        Console.WriteLine("Saving graph G2 to file...");
        GraphService.SaveGraph(g, G2_FileName);
    }
}

[MemoryDiagnoser(false)]
public class BFSBenchmarks_G1
{
    public static List<uint>[] loadedGraph;

    [GlobalSetup]
    public void Setup()
    {
        string FileName = "g1_adjacency_list.bin";
        Console.WriteLine($"Loading graph {FileName} from file...");
        GC.TryStartNoGCRegion(1_073_741_824L * 2);
        loadedGraph = GraphService.LoadGraph(FileName);
        GC.EndNoGCRegion();
        GC.Collect();
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
    }

    [GlobalCleanup]
    public void Clenup()
    {
        
    }

    [Benchmark]
    public void Sequential() => Searchers.BFS_Sequential(loadedGraph, 0);

    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void Parallel(int maxThreads) => Searchers.BFS_Parallel(loadedGraph, 0, maxThreads);
        
    [Benchmark]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    [Arguments(16)]
    public void Distributed(int numWorkers) => Searchers.BFS_Distributed(loadedGraph, 0, numWorkers);
}

[MemoryDiagnoser(false)]
public class BFSBenchmarks_G2
{
    public static List<uint>[] loadedGraph;

    [GlobalSetup]
    public void Setup()
    {
        string FileName = "g2_adjacency_list.bin";
        Console.WriteLine($"Loading graph {FileName} from file...");
        //GC.TryStartNoGCRegion(1_073_741_824L * 10);
        loadedGraph = GraphService.LoadGraph(FileName);
        //GC.KeepAlive(loadedGraph);
        //GC.EndNoGCRegion();
        //GC.Collect();
        //GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
    }

    //[Benchmark]
    //public void Sequential() => Searchers.BFS_Sequential(loadedGraph, 0);

    //[Benchmark]
    //[Arguments(2)]
    //[Arguments(4)]
    //[Arguments(8)]
    //[Arguments(12)]
    //[Arguments(16)]
    //public void Parallel(int maxThreads) => Searchers.BFS_Parallel(loadedGraph, 0, maxThreads);

    [Benchmark]
    //[Arguments(2)]
    //[Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    //[Arguments(16)]
    public void Distributed(int numWorkers) => Searchers.BFS_Distributed(loadedGraph, 0, numWorkers);
}


