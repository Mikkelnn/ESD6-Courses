using BFSAlgo.Distributed.Network;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Xml.Linq;

namespace BFSAlgo.Distributed
{
    public class Coordinator
    {
        private readonly ITcpListener _listener;
        private readonly INetworkStreamFactory _streamFactory;
        private readonly INetworkHelper _networkHelper;

        public IPEndPoint ListeningOn => (IPEndPoint)_listener.LocalEndpoint;

        private readonly List<INetworkStream> connectedWorkers = new();       
        private Func<Task>? _onWorkerConnectedAsync;

        public int ConnectedWorkers => connectedWorkers.Count;


        public Coordinator(IPAddress bindAddress, int port)
        {
            _listener = new TcpListenerWrapper(bindAddress, port);
            _networkHelper = new NetworkHelper();
            _streamFactory = new NetworkStreamFactory();
        }

        public Coordinator(ITcpListener listener, INetworkStreamFactory streamFactory, INetworkHelper networkHelper)
        {
            _listener = listener;
            _streamFactory = streamFactory;
            _networkHelper = networkHelper;
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
                var workerStream = _streamFactory.Create(tcpClient);
                connectedWorkers.Add(workerStream);
                
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

            //var totalLoopTime = Stopwatch.StartNew();
            while (partitionedFrontier.Any(x => x.Count != 0))
            {
                //sendFronteris.Start();
                //Send frontier information to all workers
                await SendNewFrontier(streams, visitedGlobal, partitionedFrontier);
                //sendFronteris.Stop();

                for (int i = 0; i < partitionedFrontier.Length; i++)
                    partitionedFrontier[i].Clear();

                //rxWait.Start();
                var receiveTasks = streams.Select(_networkHelper.ReceiveUintArrayAsync);
                var results = Task.WhenEach(receiveTasks); // Wait for workers partial frontiers
                //rxWait.Stop();
                //Console.WriteLine($"rxWait inc: {rxWait.ElapsedMilliseconds} ms");

                //prepNext.Start();
                // sequentially handle workers frontiers as thy respond
                await foreach (var result in results)
                    foreach (var node in await result)
                        if (visitedGlobal.SetIfNot(node))
                            partitionedFrontier[node % workerCount].Add(node);
                //prepNext.Stop();
            }
            //totalLoopTime.Stop();

            // Tell workers to stop
            await TerminateWorkers(streams);

            //Console.WriteLine($"Timings (ms) => " +
            //    $"partionAndSend: {partionAndSend.ElapsedMilliseconds} (partion: {partion.ElapsedMilliseconds}, sendPartion: {sendPartion.ElapsedMilliseconds}), " +
            //    $"SendFronteris: {sendFronteris.ElapsedMilliseconds}, " +
            //    $"RX Wait: {rxWait.ElapsedMilliseconds}, Prep: {prepNext.ElapsedMilliseconds}, " +
            //    $"TotalLoop: {totalLoopTime.ElapsedMilliseconds}");


            return visitedGlobal;
        }

        private async Task SendPartitions(INetworkStream[] streams, List<uint>[] partitioned, List<uint>[] graph)
        {
            if (streams.Length != partitioned.Length) 
                throw new ArgumentException("Not same amount of partitions as strams");

            var senders = streams.Select((stream, i) => Task.Run(() => _networkHelper.SendGraphPartitionAsync(streams[i], partitioned[i], graph)));

            await Task.WhenAll(senders);
        }

        private async Task SendNewFrontier(INetworkStream[] streams, Bitmap visitedGlobal, List<uint>[] frontier)
        {
            var currentGlobalVisited = visitedGlobal.AsReadOnlyMemory;

            var sendTasks = streams.Select(async (stream, i) =>
            {
                await _networkHelper.SendByteArrayAsync(stream, frontier[i].ToReadOnlyMemory());
                await _networkHelper.SendByteArrayAsync(stream, currentGlobalVisited);
                await _networkHelper.FlushStreamAsync(stream);
            });
            await Task.WhenAll(sendTasks);  // Wait for all sends to complete
        }

        private async Task TerminateWorkers(INetworkStream[] streams)
        {
            var sendTasksStop = streams.Select(async stream =>
            {
                await _networkHelper.SendByteArrayAsync(stream, null);
                await _networkHelper.FlushStreamAsync(stream);
            });
            await Task.WhenAll(sendTasksStop);  // Wait for all sends to complete
        }
    }

}
