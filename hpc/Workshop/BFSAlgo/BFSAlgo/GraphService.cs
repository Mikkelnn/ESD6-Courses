using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo
{
    internal class GraphService
    {
        public static List<uint>[] GenerateGraph(uint nodeCount, uint maxEdgesPerNode)
        {
            Console.WriteLine($"nodeCount: {nodeCount} maxEdgesPerNode: {maxEdgesPerNode}");

            var rand = Random.Shared;
            var graph = new List<uint>[nodeCount];
            var edgeSet = new HashSet<(uint, uint)>();

            for (uint i = 0; i < nodeCount; i++)
                graph[i] = new List<uint>();

            for (uint i = 0; i < nodeCount; i++)
            {
                if (i % (nodeCount / 100) == 0)
                    Console.WriteLine($"i = {i}");

                uint localMaxEdgeCount = (uint)rand.NextInt64(maxEdgesPerNode);
                while (graph[i].Count < localMaxEdgeCount)
                {
                    uint target = (uint)rand.NextInt64(nodeCount);
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

        public static void SaveGraph(List<uint>[] graph, string path)
        {
            using (var writer = new StreamWriter(path, false))
            {
                for (uint i = 0; i < graph.Length; i++)
                {
                    writer.WriteLine($"{i}:{string.Join(",", graph[i])}");
                }
            }
        }

        public static List<uint>[] LoadGraph(string path)
        {
            var lines = File.ReadAllLines(path);
            var graph = new List<uint>[lines.Length];

            for (uint i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split(':');
                var neighbors = parts.Length > 1 && !string.IsNullOrEmpty(parts[1])
                    ? Array.ConvertAll(parts[1].Split(','), uint.Parse)
                    : Array.Empty<uint>();

                graph[i] = new List<uint>(neighbors);
            }

            return graph;
        }
    }
}
