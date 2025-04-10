using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

class Program
{
    const int NodeCount = 10000;
    const int MaxEdgesPerNode = 10;

    static List<int>[] GenerateGraph()
    {
        var rand = new Random();
        var graph = new List<int>[NodeCount];
        var edgeSet = new HashSet<(int, int)>();

        for (int i = 0; i < NodeCount; i++)
            graph[i] = new List<int>();

        for (int i = 0; i < NodeCount; i++)
        {
            int edgeCount = rand.Next(1, MaxEdgesPerNode + 1);
            for (int j = 0; j < edgeCount; j++)
            {
                int target = rand.Next(NodeCount);
                if (target != i)
                {
                    var edge = (Math.Min(i, target), Math.Max(i, target));
                    if (!edgeSet.Contains(edge))
                    {
                        graph[i].Add(target);
                        graph[target].Add(i);
                        edgeSet.Add(edge);
                    }
                }
            }
        }

        return graph;
    }

    static void BFS(List<int>[] graph, int startNode)
    {
        var visited = new bool[NodeCount];
        var queue = new Queue<int>();
        visited[startNode] = true;
        queue.Enqueue(startNode);

        while (queue.Count > 0)
        {
            int node = queue.Dequeue();
            foreach (int neighbor in graph[node])
            {
                if (!visited[neighbor])
                {
                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    static void SaveAdjacencyList(List<int>[] graph, string filePath)
    {
        using (StreamWriter writer = new StreamWriter(filePath, false)) // false = overwrite
        {
            for (int i = 0; i < graph.Length; i++)
            {
                writer.WriteLine($"{i}: {string.Join(", ", graph[i])}");
            }
        }
    }

    static void Main()
    {
        Console.WriteLine("Generating graph...");
        var graph = GenerateGraph();

        Console.WriteLine("Starting BFS...");
        Stopwatch sw = Stopwatch.StartNew();
        BFS(graph, 0);
        sw.Stop();

        Console.WriteLine($"BFS completed in {sw.ElapsedMilliseconds} ms");

        string fileName = "adjacency_list.txt";
        SaveAdjacencyList(graph, fileName);
        Console.WriteLine($"Adjacency list saved to: {fileName}");
    }
}
