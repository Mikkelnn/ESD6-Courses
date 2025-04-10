using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo
{
    internal class GraphService
    {
        public static List<int>[] GenerateGraph(int nodeCount, int maxEdgesPerNode)
        {
            var rand = new Random();
            var graph = new List<int>[nodeCount];
            var edgeSet = new HashSet<(int, int)>();

            for (int i = 0; i < nodeCount; i++)
                graph[i] = new List<int>();

            for (int i = 0; i < nodeCount; i++)
            {
                int localMaxEdgeCount = Random.Shared.Next(maxEdgesPerNode);
                while (graph[i].Count < localMaxEdgeCount)
                {
                    int target = rand.Next(nodeCount);
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

        public static void SaveGraph(List<int>[] graph, string path)
        {
            using (var writer = new StreamWriter(path, false))
            {
                for (int i = 0; i < graph.Length; i++)
                {
                    writer.WriteLine($"{i}:{string.Join(",", graph[i])}");
                }
            }
        }

        public static List<int>[] LoadGraph(string path)
        {
            var lines = File.ReadAllLines(path);
            var graph = new List<int>[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split(':');
                var neighbors = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                    ? Array.ConvertAll(parts[1].Split(','), int.Parse)
                    : Array.Empty<int>();

                graph[i] = new List<int>(neighbors);
            }

            return graph;
        }
    }
}
