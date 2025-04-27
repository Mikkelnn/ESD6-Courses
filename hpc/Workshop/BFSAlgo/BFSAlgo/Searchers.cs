using BFSAlgo.Distributed;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo
{
    public class Searchers
    {
        public static void BFS_Sequential(List<uint>[] graph, uint startNode)
        {
            var visited = new Bitmap(graph.Length);
            var queue = new Queue<uint>();
            visited.Set(startNode);
            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                long node = queue.Dequeue();
                foreach (var neighbor in graph[node])
                {
                    if (!visited.Get(neighbor))
                    {
                        visited.Set(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        public static void BFS_Parallel(List<uint>[] graph, uint startNode, int maxThreads)
        {
            int numNodes = graph.Length;
            var visited = new int[numNodes]; // 0: not visited, 1: visited
            var currentFrontier = new ConcurrentQueue<uint>();
            currentFrontier.Enqueue(startNode);
            Interlocked.Exchange(ref visited[startNode], 1);

            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads }; // e.g., 4

            while (!currentFrontier.IsEmpty)
            {
                var nextFrontier = new ConcurrentQueue<uint>();
                var frontierArray = currentFrontier.ToArray();

                Parallel.ForEach(frontierArray, options, node =>
                {
                    foreach (var neighbor in graph[node])
                    {
                        // Atomically mark visited
                        if (Interlocked.CompareExchange(ref visited[neighbor], 1, 0) == 0)
                        {
                            nextFrontier.Enqueue(neighbor);
                        }
                    }
                });

                currentFrontier = nextFrontier;
            }
        }

        public static void BFS_Parallel_V2(List<uint>[] graph, uint startNode, int maxThreads)
        {
            int numNodes = graph.Length;
            var visited = new int[numNodes];
            var currentFrontier = new List<uint>(capacity: 1024 * 100);
            var nextFrontierBuffer = new List<uint>[maxThreads];
            for (int i = 0; i < maxThreads; i++)
                nextFrontierBuffer[i] = nextFrontierBuffer[i] ?? new List<uint>(capacity: 1024 * 100);

            // Initialize visited and frontier
            Interlocked.Exchange(ref visited[startNode], 1);
            currentFrontier.Add(startNode);

            int level = 0;

            while (currentFrontier.Count > 0)
            {
                //Console.WriteLine($"Level {level++}: Frontier size = {currentFrontier.Count}");

                // Initialize thread-local frontiers                
                Array.ForEach(nextFrontierBuffer, f => f.Clear());

                int chunkSize = (currentFrontier.Count + maxThreads - 1) / maxThreads;

                Parallel.For(0, maxThreads, new ParallelOptions { MaxDegreeOfParallelism = maxThreads }, i =>
                {
                    int start = i * chunkSize;
                    int end = Math.Min(start + chunkSize, currentFrontier.Count);
                    var localFrontier = nextFrontierBuffer[i];

                    for (int j = start; j < end; j++)
                    {
                        uint node = currentFrontier[j];
                        foreach (var neighbor in graph[node])
                        {
                            if (Interlocked.CompareExchange(ref visited[neighbor], 1, 0) == 0)
                            {
                                localFrontier.Add(neighbor);
                            }
                        }
                    }
                });

                // Merge all thread-local next frontier lists
                currentFrontier.Clear();
                foreach (var list in nextFrontierBuffer)
                {
                    currentFrontier.AddRange(list);
                }
            }
        }

        public static void BFS_Parallel_V3(List<uint>[] graph, uint startNode, int maxThreads)
        {
            int numNodes = graph.Length;
            var visited = new Bitmap(numNodes); // 0: not visited, 1: visited
            visited.Set(startNode);
            
            var currentFrontier = new Queue<uint>();
            currentFrontier.Enqueue(startNode);

            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads }; // e.g., 4

            object _lock = new();

            while (currentFrontier.Count > 0)
            {                
                var frontierArray = currentFrontier.ToArray();
                currentFrontier.Clear();

                Parallel.ForEach(frontierArray, options, node =>
                {
                    foreach (var neighbor in graph[node])
                    {
                        // Atomically mark visited
                        if (!visited.Get(neighbor))
                        {
                            lock (_lock)
                            {
                                visited.Set(neighbor);
                                currentFrontier.Enqueue(neighbor);
                            }
                        }
                    }
                });

                //currentFrontier = nextFrontier;
            }
        }

        public static void BFS_Parallel_V3_noLock(List<uint>[] graph, uint startNode, int maxThreads)
        {
            int numNodes = graph.Length;
            var visited = new Bitmap(numNodes); // 0: not visited, 1: visited
            visited.Set(startNode);

            var currentFrontier = new Queue<uint>();
            currentFrontier.Enqueue(startNode);

            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads }; // e.g., 4

            //object _lock = new();

            while (currentFrontier.Count > 0)
            {
                var frontierArray = currentFrontier.ToArray();
                currentFrontier.Clear();

                Parallel.ForEach(frontierArray, options, node =>
                {
                    foreach (var neighbor in graph[node])
                    {
                        // Atomically mark visited
                        if (!visited.Get(neighbor))
                        {
                            //lock (_lock)
                            //{
                                visited.Set(neighbor);
                                currentFrontier.Enqueue(neighbor);
                            //}
                        }
                    }
                });

                //currentFrontier = nextFrontier;
            }
        }

        public static void BFS_Parallel_V3_Partition(List<uint>[] graph, uint startNode, int maxThreads)
        {
            int numNodes = graph.Length;
            var visited = new Bitmap(numNodes); // 0: not visited, 1: visited
            visited.Set(startNode);

            var currentFrontier = new Queue<uint>();
            currentFrontier.Enqueue(startNode);

            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads }; // e.g., 4

            object _lock = new();

            while (currentFrontier.Count > 0)
            {
                var frontierArray = currentFrontier.ToArray();
                currentFrontier.Clear();

                var parts = Partitioner.Create(frontierArray).GetPartitions(maxThreads);

                Parallel.ForEach(parts, nodes =>
                {
                    while (nodes.MoveNext())
                    {
                        foreach (var neighbor in graph[nodes.Current])
                        {
                            // Atomically mark visited
                            if (!visited.Get(neighbor))
                            {
                                lock (_lock)
                                {
                                    visited.Set(neighbor);
                                    currentFrontier.Enqueue(neighbor);
                                }
                            }
                        }
                    }
                });

                //currentFrontier = nextFrontier;
            }
        }

        public static async Task BFS_Distributed(List<uint>[] graph, uint startNode, int numWorkers)
        {
            var coordinator = new Coordinator(graph, startNode, numWorkers);
            await coordinator.Run();
        }
    }
}
