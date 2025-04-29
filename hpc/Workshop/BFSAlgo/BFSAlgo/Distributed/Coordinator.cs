using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime;

namespace BFSAlgo.Distributed
{
    public class Coordinator
    {
        private readonly TcpListener _listener;
        public IPEndPoint ListeningOn => (IPEndPoint)_listener.LocalEndpoint;

        private readonly List<INetworkStream> connectedWorkers = new();
        public int ConnectedWorkers => connectedWorkers.Count;

        private Func<Task>? _onWorkerConnectedAsync;

        public Coordinator(IPAddress bindAddress, int port)
        {
            _listener = new TcpListener(bindAddress, port);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            await HandleWorkerConnect();
        }

        private async Task HandleWorkerConnect()
        {
            while (true)
            {
                var tcpClient = await _listener.AcceptTcpClientAsync();
                var worker = new NetworkStreamWrapper(tcpClient);
                connectedWorkers.Add(worker);
                
                if (_onWorkerConnectedAsync != null)
                    await _onWorkerConnectedAsync.Invoke();
            }
        }

        public Task WaitForWorkerCountAsync(int numWorkers)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _onWorkerConnectedAsync = async () =>
            {
                if (ConnectedWorkers >= numWorkers)
                {
                    tcs.TrySetResult(true);
                    _onWorkerConnectedAsync = null; // Unsubscribe after done
                }
            };

            // In case workers already connected
            _ = _onWorkerConnectedAsync.Invoke();

            return tcs.Task;
        }

        public async Task<Bitmap> RunAsync(List<uint>[] graph, uint startNode)
        {
            var streams = connectedWorkers.ToArray();
            int workerCount = streams.Length;


            //var partionAndSend = Stopwatch.StartNew();
            //var partion = Stopwatch.StartNew();
            List<uint>[] partitioned = GraphPartitioner.Partition(graph, workerCount);
            //partion.Stop();
            //var sendPartion = Stopwatch.StartNew();
            await SendPartitions(streams, partitioned, graph);
            //sendPartion.Stop();
            //partionAndSend.Stop();

            var visitedGlobal = new Bitmap(graph.Length);
            visitedGlobal.Set(startNode);

            var partitionedFrontier = new List<uint>[workerCount];
            for (int i = 0; i < partitionedFrontier.Length; i++)
                partitionedFrontier[i] ??= new List<uint>();

            // add ninitial frontier to accosiated worker
            partitionedFrontier[startNode % workerCount] = new List<uint>() { startNode };

            //var sendFronteris = new Stopwatch();
            //var rxWait = new Stopwatch();
            //var prepNext = new Stopwatch();

            //var asMemory = new Stopwatch();
            //var totalLoopTime = Stopwatch.StartNew();
            while (partitionedFrontier.Any(x => x.Count != 0))
            {
                //sendFronteris.Start();
                // Send frontier information to all workers
                await SendNewFrontier(streams, visitedGlobal, partitionedFrontier);
                //sendFronteris.Stop();

                for (int i = 0; i < partitionedFrontier.Length; i++)
                    partitionedFrontier[i].Clear();

                //rxWait.Start();
                var receiveTasks = streams.Select(NetworkHelper.ReceiveUintArrayAsync);
                var results = await Task.WhenAll(receiveTasks);  // Wait for all receives to complete                
                //rxWait.Stop();
                //Console.WriteLine($"rxWait inc: {rxWait.ElapsedMilliseconds} ms");

                //prepNext.Start();
                for (int i = 0; i < workerCount; i++)
                    foreach (var node in results[i])
                        if (visitedGlobal.SetIfNot(node))
                            partitionedFrontier[node % workerCount].Add(node);
                //prepNext.Stop();
            }
            //totalLoopTime.Stop();

            // Tell workers to stop
            await TerminateWorkers(streams);

            //Console.WriteLine($"Timings (ms) => " +
            //    $"partionAndSend: {partionAndSend.ElapsedMilliseconds} (partion: {partion.ElapsedMilliseconds}, sendPartion: {sendPartion.ElapsedMilliseconds}), " +
            //    $"SendFronteris: {sendFronteris.ElapsedMilliseconds} (asMemory: {asMemory.ElapsedMilliseconds}), " +
            //    $"RX Wait: {rxWait.ElapsedMilliseconds}, Prep: {prepNext.ElapsedMilliseconds}, " +
            //    $"TotalLoop: {totalLoopTime.ElapsedMilliseconds}");


            return visitedGlobal;
        }

        private async Task SendPartitions(INetworkStream[] streams, List<uint>[] partitioned, List<uint>[] graph)
        {
            if (streams.Length != partitioned.Length) 
                throw new ArgumentException("Not same amount of partitions as strams");

            var senders = streams.Select((stream, i) => Task.Run(() => NetworkHelper.SendGraphPartitionAsync(streams[i], partitioned[i], graph)));

            await Task.WhenAll(senders);
        }

        private static async Task SendNewFrontier(INetworkStream[] streams, Bitmap visitedGlobal, List<uint>[] frontier)
        {
            var currentGlobalVisited = visitedGlobal.AsReadOnlyMemory;

            var sendTasks = streams.Select(async (stream, i) =>
            {
                await NetworkHelper.SendDataAsync(stream, frontier[i].ToReadOnlyMemory());
                await NetworkHelper.SendDataAsync(stream, currentGlobalVisited);
                await NetworkHelper.FlushStreamAsync(stream);
            });
            await Task.WhenAll(sendTasks);  // Wait for all sends to complete
        }

        private async Task TerminateWorkers(INetworkStream[] streams)
        {
            var sendTasksStop = streams.Select(async stream =>
            {
                await NetworkHelper.SendDataAsync(stream, null);
                await NetworkHelper.FlushStreamAsync(stream);
            });
            await Task.WhenAll(sendTasksStop);  // Wait for all sends to complete
        }
    }

}
