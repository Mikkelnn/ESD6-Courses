using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BFSAlgo;

class Program
{
    const int NodeCount = 100000;
    const int TargetTotalEdges = 67108864;
    const int EdgesPerNode = TargetTotalEdges / NodeCount;
    const string FileName = "adjacency_list.txt";

    static void Main(string[] args)
    {
        List<int>[] graph;

        if (File.Exists(FileName))
        {
            Console.WriteLine("Loading graph from file...");
            graph = GraphService.LoadGraph(FileName);
        }
        else
        {
            Console.WriteLine("Generating graph...");
            graph = GraphService.GenerateGraph(NodeCount, EdgesPerNode);
            Console.WriteLine("Saving graph to file...");
            GraphService.SaveGraph(graph, FileName);
        }

        Console.WriteLine("Running BFS...");
        Stopwatch sw = Stopwatch.StartNew();
        Searchers.BFS_Sequential(graph, 0);
        sw.Stop();

        Console.WriteLine($"BFS completed in {sw.ElapsedMilliseconds} ms.");
    }
}
