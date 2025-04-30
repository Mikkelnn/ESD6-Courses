using System.Net;
using System.Collections.Concurrent;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using BFSAlgo.Distributed.Network;

namespace BFSAlgo.Distributed
{
    public class Worker
    {
        private readonly IPAddress coordinatorAddress;
        private readonly int coordinatorPort;
        private INetworkStream _stream;
        private readonly INetworkHelper _networkHelper;
        private Bitmap visited;

        // Will point to a array of pointers, with a length equal the global node count
        // we only save a pointer to the neighbors for each node and thus not too large - but fast for lookup
        // if a worker, worskcase, try to access a node it do not have, an emppty array is returned
        private ArraySegment<uint>[] partialGraph;


        public Worker(IPAddress coordinatorAddress, int coordinatorPort)
        {
            this.coordinatorAddress = coordinatorAddress;
            this.coordinatorPort = coordinatorPort;
            _networkHelper = new NetworkHelper();
        }

        // Constructor for DI/testing
        public Worker(INetworkStream stream, INetworkHelper networkHelper)
        {
            _stream = stream;
            _networkHelper = networkHelper;
        }

        public async Task Start()
        {
            _stream ??= await NetworkStreamWrapper.GetWorkerInstanceAsync(coordinatorAddress, coordinatorPort);

            (this.partialGraph, var totalNodeCount) = await _networkHelper.ReceiveGraphPartitionAsync(_stream);

            visited = new Bitmap(totalNodeCount);

            await RunMainLoop();

            _stream.Close();
        }

        private async Task RunMainLoop()
        {
            while (true)
            {
                var frontier = await _networkHelper.ReceiveUintArrayAsync(_stream);
                if (frontier == null) break;

                var globalVisited = await _networkHelper.ReceiveByteArrayAsync(_stream);
                visited.OverwriteFromByteArray(globalVisited);

                var nextFrontier = FrontierSearchers.SearchFrontier(frontier, partialGraph, visited);
                //var nextFrontier = FrontierSearchers.SearchFrontierParallel(frontier, partialGraph, visited, maxThreads: 4);

                var data = nextFrontier.ToReadOnlyMemory();
                await _networkHelper.SendByteArrayAsync(_stream, data);
                await _networkHelper.FlushStreamAsync(_stream);
            }
        }
    }
}
