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
            GC.TryStartNoGCRegion(1_073_741_824L * 5);

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

            GC.EndNoGCRegion();
            GC.Collect();
        }

        //public static void BFS_Parallel(List<uint>[] graph, uint startNode, int maxThreads)
        //{
        //    int numNodes = graph.Length;
        //    var visited = new int[numNodes]; // 0: not visited, 1: visited
        //    var currentFrontier = new ConcurrentQueue<uint>();
        //    currentFrontier.Enqueue(startNode);
        //    Interlocked.Exchange(ref visited[startNode], 1);

        //    var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads }; // e.g., 4

        //    while (!currentFrontier.IsEmpty)
        //    {
        //        var nextFrontier = new ConcurrentQueue<uint>();
        //        var frontierArray = currentFrontier.ToArray();

        //        Parallel.ForEach(frontierArray, options, node =>
        //        {
        //            foreach (var neighbor in graph[node])
        //            {
        //                // Atomically mark visited
        //                if (Interlocked.CompareExchange(ref visited[neighbor], 1, 0) == 0)
        //                {
        //                    nextFrontier.Enqueue(neighbor);
        //                }
        //            }
        //        });

        //        currentFrontier = nextFrontier;
        //    }
        //}

        public static void BFS_Parallel(List<uint>[] graph, uint startNode, int maxThreads)
        {
            GC.TryStartNoGCRegion(1_073_741_824L * 5);

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
            }

            GC.EndNoGCRegion();
            GC.Collect();
        }

        public static async Task BFS_Distributed(List<uint>[] graph, uint startNode, int numWorkers)
        {
            GC.TryStartNoGCRegion(1_073_741_824L * 10);

            var coordinator = new Coordinator(graph, startNode, numWorkers);
            await coordinator.Run();

            GC.EndNoGCRegion();
            GC.Collect();
        }
    }
}
