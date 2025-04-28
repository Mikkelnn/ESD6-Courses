using System.Net;
using System.Collections.Concurrent;

namespace BFSAlgo.Distributed
{
    public class Worker
    {
        private readonly int id;
        private Bitmap visited;
        private Dictionary<uint, uint[]> partialGraph;
        private readonly int port;

        private INetworkStream stream;

        public Worker(int id, int port)
        {
            this.id = id;
            this.port = port;
        }

        // Constructor for DI/testing
        public Worker(int id, int port, INetworkStream stream) : this(id, port)
        {
            this.stream = stream;
        }

        public async Task Start()
        {
            stream ??= await NetworkStreamWrapper.GetWorkerInstanceAsync(IPAddress.Loopback, port);

            (this.partialGraph, var totalNodeCount) = await NetworkHelper.ReceiveGraphPartitionAsync(stream);

            visited = new Bitmap(totalNodeCount);

            await RunMainLoop();

            stream.Close();
        }

        private async Task RunMainLoop()
        {
            while (true)
            {
                var frontier = await NetworkHelper.ReceiveUintArrayAsync(stream);
                if (frontier == null) break;

                //var globalVisited = await NetworkHelper.ReceiveByteArrayAsync(stream);
                //visited.OverwriteFromByteArray(globalVisited);

                var nextFrontier = SearchFrontier(frontier);
                //var nextFrontier = SearchFrontierParallel(frontier, maxThreads: 4);

                var data = nextFrontier.ToReadOnlyMemory();
                await NetworkHelper.SendDataAsync(stream, data);
                await NetworkHelper.FlushStreamAsync(stream);
            }
        }

        private List<uint> SearchFrontier(Span<uint> frontier)
        {
            List<uint> nextFrontier = new List<uint>();
            if (frontier.IsEmpty) return nextFrontier;

            foreach (var node in frontier)
            {
                if (!partialGraph.TryGetValue(node, out var neighbors)) continue;

                foreach (var neighbor in neighbors)
                {
                    if (visited.SetIfNot(neighbor))
                        nextFrontier.Add(neighbor);
                }
            }

            //for (int i = 0, fl = frontier.Count; i < fl; i++)
            //{
            //    var neighbors = partialGraph[frontier[i]];

            //    for (int j = 0, length = neighbors.Length; j < length; j++)
            //    {
            //        uint neighbor = neighbors[j];
            //        if (!visited.SetIfNot(neighbor))
            //            nextFrontier.Add(neighbor);
            //    }
            //}

            return nextFrontier;
        }

        //private List<uint> SearchFrontierParallel(List<uint> frontier, int maxThreads)
        //{
        //    ConcurrentBag<uint> nextFrontier = new ConcurrentBag<uint>();
        //    if (frontier.Count == 0) return nextFrontier.ToList();


        //    var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads }; // e.g., 4
        //    //object _lock = new();


        //    Parallel.ForEach(frontier, options, node =>
        //    {
        //        //if (!partialGraph.TryGetValue(node, out var neighbors)) return;

        //        var neighbors = partialGraph[node];

        //        for (int i = 0, length = neighbors.Length; i < length; i++)
        //        {
        //            uint neighbor = neighbors[i];

        //            // Atomically mark visited
        //            if (visited.SetIfNot(neighbor))
        //                nextFrontier.Add(neighbor);                    
        //        }
        //    });

        //    return nextFrontier.ToList();

        //}
    }
}
