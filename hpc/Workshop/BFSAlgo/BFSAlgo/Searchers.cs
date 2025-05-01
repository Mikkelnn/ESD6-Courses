using BFSAlgo.Distributed;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;

namespace BFSAlgo
{
    public class GraphSearchers
    {
        public static Bitmap BFS_Sequential(List<uint>[] graph, uint startNode)
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
                    if (visited.SetIfNot(neighbor))
                        queue.Enqueue(neighbor);                    
                }
            }

            return visited;
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

        public static Bitmap BFS_ParallelSharedMemory(List<uint>[] graph, uint startNode, int maxThreads)
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
            }

            return visited;
        }

        public static Bitmap BFS_Distributed(List<uint>[] graph, uint startNode, int numWorkers, int millisecondsTimeout = -1)
        {
            IPAddress address = IPAddress.Loopback;

            //Stopwatch setup = Stopwatch.StartNew();
            // start server
            var coordinator = new Coordinator(address, port: 0); // use next available port
            var runnerTask = coordinator.StartAsync();
            
            var coordinatorEndpoint = coordinator.ListeningOn;
            //setup.Stop();

            //Stopwatch spawnWorkers = Stopwatch.StartNew();
            // spawn workers
            var workerTasks = new Task[numWorkers];
            for (int i = 0; i < numWorkers; i++)
            {
                workerTasks[i] = Task.Run(async () =>
                {
                    var worker = new Worker(coordinatorEndpoint.Address, coordinatorEndpoint.Port);
                    await worker.Start();
                });
            }
            //spawnWorkers.Stop();

            //Stopwatch waitConnect = Stopwatch.StartNew();
            // wait for workers to connect
            bool completed = coordinator.WaitForWorkerCountAsync(numWorkers).Wait(millisecondsTimeout);
            if (!completed) throw new Exception("Timeout waiting for workers!");
            //waitConnect.Stop();

            //Stopwatch searcch = Stopwatch.StartNew();
            var visited = coordinator.RunAsync(graph, startNode);

            completed = visited.Wait(millisecondsTimeout);
            if (!completed) throw new Exception("Timeout searching!");
            //searcch.Stop();

            //Console.WriteLine($"Setup: {setup.ElapsedMilliseconds} ms " +
            //    $"Spawn workers: {spawnWorkers.ElapsedMilliseconds} ms " +
            //    $"Wait connect: {waitConnect.ElapsedMilliseconds} ms " +
            //    $"Searcch: {searcch.ElapsedMilliseconds} ms");

            return visited.Result;
        }
    }
}
