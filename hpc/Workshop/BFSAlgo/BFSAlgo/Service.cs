using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo
{
    public class GraphService
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
                if (i % Math.Max(1, (int)(nodeCount * 0.01)) == 0)
                    Console.WriteLine($"i = {i}");

                uint localMaxEdgeCount = (uint)rand.NextInt64(maxEdgesPerNode);
                while (graph[i].Count < localMaxEdgeCount)
                {
                    uint target = (uint)rand.NextInt64(nodeCount);
                    if (target != i)
                    {
                        var edge = (Math.Min(i, target), Math.Max(i, target)); // This line makes sure that there is no duplicate edge generation.
                        if (!edgeSet.Contains(edge))//E.g. if the edge 3,4 already exists 4,3 wont be made
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

        public static void GenerateGraphToDisk(string graphPath, uint nodeCount, uint maxEdgesPerNode)
        {
            Console.WriteLine($"nodeCount: {nodeCount} maxEdgesPerNode: {maxEdgesPerNode}");

            var rand = Random.Shared;
            long[] indexOffsets = new long[nodeCount];

            using var graphStream = new FileStream(graphPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var writer = new BinaryWriter(graphStream);

            writer.Write(nodeCount); // header

            var startTime = DateTime.UtcNow;
            for (uint i = 0; i < nodeCount; i++)
            {
                if (i % Math.Max(1, nodeCount * 0.01) == 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    double avgTimePerNode = elapsed.TotalSeconds / (i + 1);
                    double remaining = (nodeCount - i - 1) * avgTimePerNode;

                    Console.WriteLine($"Processing node {i}/{nodeCount} | Elapsed: {elapsed:hh\\:mm\\:ss} | ETA: {TimeSpan.FromSeconds(remaining):hh\\:mm\\:ss}");
                }

                // Track current file position for this node
                indexOffsets[i] = writer.BaseStream.Position;

                HashSet<uint> neighbors = new();

                uint localMaxEdgeCount = (uint)rand.NextInt64(maxEdgesPerNode);
                while (neighbors.Count < localMaxEdgeCount)
                {
                    uint target = (uint)rand.NextInt64(nodeCount);
                    if (target == i || neighbors.Contains(target))
                        continue;

                    if (target > i)
                    {
                        // Forward edge - always write
                        neighbors.Add(target);
                    }
                    else
                    {
                        // Reverse edge - check if already written in target's list
                        if (!EdgeExists(graphStream, indexOffsets[target], i))
                        {
                            neighbors.Add(target);
                        }
                    }
                }

                writer.Write(neighbors.Count);
                foreach (var neighbor in neighbors)
                {
                    writer.Write(neighbor);
                }
            }

            //// Write index file separately
            //using var indexStream = new FileStream(indexPath, FileMode.Create, FileAccess.Write);
            //using var indexWriter = new BinaryWriter(indexStream);
            //foreach (var offset in indexOffsets)
            //{
            //    indexWriter.Write(offset);
            //}

            Console.WriteLine("Graph written to disk.");
        }

        private static bool EdgeExists(FileStream graphStream, long offset, uint targetValue)
        {
            var originalPosition = graphStream.Position;

            graphStream.Seek(offset, SeekOrigin.Begin);
            using var reader = new BinaryReader(graphStream, System.Text.Encoding.Default, leaveOpen: true);

            int neighborCount = reader.ReadInt32();
            for (int i = 0; i < neighborCount; i++)
            {
                if (reader.ReadUInt32() == targetValue)
                {
                    graphStream.Seek(originalPosition, SeekOrigin.Begin);
                    return true;
                }
            }

            graphStream.Seek(originalPosition, SeekOrigin.Begin);
            return false;
        }

        public static void SaveGraph(List<uint>[] graph, string path)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(graph.Length); // Write number of nodes
                foreach (var neighbors in graph)
                {
                    writer.Write(neighbors.Count); // Write number of neighbors
                    foreach (var neighbor in neighbors)
                        writer.Write(neighbor); // Write each neighbor
                }
            }
        }

        public static List<uint>[] LoadGraph(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                int nodeCount = reader.ReadInt32();
                var graph = new List<uint>[nodeCount];
                for (int i = 0; i < nodeCount; i++)
                {
                    int neighborCount = reader.ReadInt32();
                    graph[i] = new List<uint>(neighborCount);
                    for (int j = 0; j < neighborCount; j++)
                        graph[i].Add(reader.ReadUInt32());
                }
                return graph;
            }
        }

        public static void SaveGraphText(List<uint>[] graph, string path)
        {
            using (var writer = new StreamWriter(path, false))
            {
                for (uint i = 0; i < graph.Length; i++)
                {
                    writer.WriteLine($"{i}:{string.Join(",", graph[i])}");
                }
            }
        }

        public static List<uint>[] LoadGraphTxt(string path)
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
