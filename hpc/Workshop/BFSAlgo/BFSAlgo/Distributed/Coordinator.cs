using System.Net;

namespace BFSAlgo.Distributed
{
    public class Coordinator
    {
        private List<uint>[] graph;
        private readonly uint startNode;
        private readonly int numWorkers;
        private readonly int basePort = 9000;

        public Coordinator(List<uint>[] graph, uint startNode, int numWorkers)
        {
            this.graph = graph;
            this.startNode = startNode;
            this.numWorkers = numWorkers;
        }

        public async Task<Bitmap> RunAsync()
        {
            // spawn workers and connect
            Task[] workerTasks = SpawnWorkers(numWorkers);
            INetworkStream[] streams = await ConnectToWorkers(numWorkers);

            List<uint>[] partitioned = GraphPartitioner.Partition(graph, numWorkers);
            await SendPartitions(streams, partitioned);

            var visitedGlobal = new Bitmap(graph.Length);
            visitedGlobal.Set(startNode);

            var partitionedFrontier = new List<uint>[numWorkers];
            for (int i = 0; i < partitionedFrontier.Length; i++)
                partitionedFrontier[i] ??= new List<uint>();

            // add ninitial frontier to accosiated worker
            partitionedFrontier[startNode % numWorkers] = new List<uint>() { startNode };

            while (partitionedFrontier.Any(x => x.Count != 0))
            {
                // Send frontier information to all workers
                await SendNewFrontier(streams, visitedGlobal, partitionedFrontier);

                for (int i = 0; i < partitionedFrontier.Length; i++)
                    partitionedFrontier[i].Clear();

                var receiveTasks = streams.Select(NetworkHelper.ReceiveUintArrayAsync);
                var results = await Task.WhenAll(receiveTasks);  // Wait for all receives to complete

                for (int i = 0; i < numWorkers; i++)
                    foreach (var node in results[i])
                        if (visitedGlobal.SetIfNot(node))
                            partitionedFrontier[node % numWorkers].Add(node);
            }

            // Tell workers to stop
            await TerminateWorkers(workerTasks, streams);

           return visitedGlobal;
        }

        private async Task SendPartitions(INetworkStream[] streams, List<uint>[] partitioned)
        {
            if (streams.Length != partitioned.Length) 
                throw new ArgumentException("Not same amount of partitions as strams");

            var senders = streams.Select((stream, i) => Task.Run(() => NetworkHelper.SendGraphPartitionAsync(streams[i], partitioned[i], graph)));

            await Task.WhenAll(senders);
        }

        private static async Task SendNewFrontier(INetworkStream[] streams, Bitmap visitedGlobal, List<uint>[] frontier)
        {
            //var currentGlobalVisited = visitedGlobal.AsReadOnlyMemory;
            //var frontierData = frontier.ToReadOnlyMemory();

            var sendTasks = streams.Select(async (stream, i) =>
            {
                await NetworkHelper.SendDataAsync(stream, frontier[i].ToReadOnlyMemory());
                //await NetworkHelper.SendDataAsync(stream, currentGlobalVisited);
                await NetworkHelper.FlushStreamAsync(stream);
            });
            await Task.WhenAll(sendTasks);  // Wait for all sends to complete
        }

        private async Task TerminateWorkers(Task[] workerTasks, INetworkStream[] streams)
        {
            var sendTasksStop = streams.Select(async stream =>
            {
                await NetworkHelper.SendDataAsync(stream, null);
                await NetworkHelper.FlushStreamAsync(stream);
            });
            await Task.WhenAll(sendTasksStop);  // Wait for all sends to complete

            for (int i = 0; i < numWorkers; i++)
            {
                streams[i].Close();
            }

            await Task.WhenAll(workerTasks);
        }

        private async Task<INetworkStream[]> ConnectToWorkers(int numWorkers)
        {
            // Connect to workers
            var streams = new INetworkStream[numWorkers];
            for (int i = 0; i < numWorkers; i++)
                streams[i] = await NetworkStreamWrapper.GetCoordinatorInstance(IPAddress.Loopback, basePort + i);

            return streams;
        }

        private Task[] SpawnWorkers(int numWorkers)
        {
            var workerTasks = new Task[numWorkers];

            for (int i = 0; i < numWorkers; i++)
            {
                int port = basePort + i;
                int id = i;
                workerTasks[i] = Task.Run(async () =>
                {
                    var worker = new Worker(id, port);
                    await worker.Start();
                });
            }

            return workerTasks;
        }
    }

}
