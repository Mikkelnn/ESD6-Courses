using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BFSAlgo;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<MyBenchmarks>();
        //new MyBenchmarks().Setup();
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
        const uint G1_NodeCount = 100_000;
        const uint G1_TargetTotalEdges = 67_108_864;

        const uint G2_NodeCount = 10_000_000;
        const uint G2_TargetTotalEdges = 671_088_640;

        const string G1_FileName = "g1_adjacency_list.txt";
        const string G2_FileName = "g2_adjacency_list.txt";

        // graph 1
        if (File.Exists(G1_FileName))
        {
            Console.WriteLine("Loading graph G1 from file...");
            g1 = GraphService.LoadGraph(G1_FileName);
        }
        else
        {
            Console.WriteLine("Generating graph G1...");
            g1 = GraphService.GenerateGraph(G1_NodeCount, maxEdgesPerNode: G1_TargetTotalEdges / G1_NodeCount);
            Console.WriteLine("Saving graph G1 to file...");
            GraphService.SaveGraph(g1, G1_FileName);
        }

        // graph 2
        if (File.Exists(G2_FileName))
        {
            Console.WriteLine("Loading graph G2 from file...");
            g2 = GraphService.LoadGraph(G2_FileName);
        }
        else
        {
            Console.WriteLine("Generating graph G2...");
            g2 = GraphService.GenerateGraph(G2_NodeCount, maxEdgesPerNode: G2_TargetTotalEdges / G2_NodeCount);
            Console.WriteLine("Saving graph G2 to file...");
            GraphService.SaveGraph(g2, G2_FileName);
        }
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

    [Benchmark(Baseline = true)]
    public void Sequential_G2() => Searchers.BFS_Sequential(g2, 0);

    [Benchmark]
    [Arguments(16)]
    public void Parallel_G2(int maxThreads) => Searchers.BFS_Parallel(g2, 0, maxThreads);

    [Benchmark]
    [Arguments(16)]
    public void Parallel_V2_G2(int maxThreads) => Searchers.BFS_Parallel_V2(g2, 0, maxThreads);
}
