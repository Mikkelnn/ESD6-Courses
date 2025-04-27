using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BFSAlgo;

public class Program
{    
    public static async Task Main(string[] args)
    {
        //GenerateGraphs();
        Console.WriteLine("loading g1..");
        var g1 = GraphService.LoadGraph("g1_adjacency_list.bin");
        Stopwatch sw = Stopwatch.StartNew();
        await Searchers.BFS_Distributed(g1, 0, numWorkers: 2);
        //Searchers.BFS_Parallel_V3(g1, 0, 12);
        //Searchers.BFS_Parallel_V3_Partition(g1, 0, 4);
        sw.Stop();
        Console.WriteLine($"Finished in: {sw.ElapsedMilliseconds} ms");


        //var summary = BenchmarkRunner.Run<MyBenchmarks>();

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
public class MyBenchmarks
{   
    public static List<uint>[] g1;
    public static List<uint>[] g2;

    [GlobalSetup]
    public void Setup()
    {
        const string G1_FileName = "g1_adjacency_list.bin";
        const string G2_FileName = "g2_adjacency_list.bin";

        // graph 1
        //Console.WriteLine("Loading graph G1 from file...");
        //g1 = GraphService.LoadGraph(G1_FileName);

        Console.WriteLine("Loading graph G2 from file...");
        g2 = GraphService.LoadGraph(G2_FileName);
    }

    //[Benchmark]
    //public void Sequential_G1() => Searchers.BFS_Sequential(g1, 0);

    //[Benchmark]
    //public void Sequential_G2() => Searchers.BFS_Sequential(g2, 0);

    //[Benchmark]
    //[Arguments(4)]
    //[Arguments(8)]
    //[Arguments(16)]
    //public void Parallel_G1(int maxThreads) => Searchers.BFS_Parallel(g1, 0, maxThreads);

    //[Benchmark]
    //[Arguments(4)]
    //[Arguments(8)]
    //[Arguments(16)]
    //public void Parallel_G2(int maxThreads) => Searchers.BFS_Parallel(g2, 0, maxThreads);

    [Benchmark]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    public void Parallel_G2_V3(int maxThreads) => Searchers.BFS_Parallel_V3(g2, 0, maxThreads);

    [Benchmark]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    public void Parallel_G2_V3_NoLock(int maxThreads) => Searchers.BFS_Parallel_V3_noLock(g2, 0, maxThreads);

    [Benchmark]
    [Arguments(4)]
    [Arguments(8)]
    [Arguments(12)]
    public void Parallel_G2_V3_Partition(int maxThreads) => Searchers.BFS_Parallel_V3_Partition(g2, 0, maxThreads);
    

    //[Benchmark(Baseline = true)]
    //public void Sequential_G2() => Searchers.BFS_Sequential(g2, 0);

    //[Benchmark]
    //[Arguments(16)]
    //public void Parallel_G2(int maxThreads) => Searchers.BFS_Parallel(g2, 0, maxThreads);

    //[Benchmark]
    //[Arguments(12)]
    //public void Parallel_V2_G2(int maxThreads) => Searchers.BFS_Parallel_V2(g2, 0, maxThreads);

    //[Benchmark]
    //[Arguments(12)]
    //public void Distributed(int numWorkers) => Searchers.BFS_Distributed(g2, 0, numWorkers, evenPartitioning: true);


    //[Benchmark]
    //[Arguments(1, true)]
    //[Arguments(1, false)]
    //[Arguments(2, true)]
    //[Arguments(2, false)]
    //public void Distributed(int numWorkers, bool evenPartitioning) => Searchers.BFS_Distributed(g1, 0, numWorkers, evenPartitioning);

}
