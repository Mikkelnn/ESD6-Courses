using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

            stream?.Close();
        }

        private async Task RunMainLoop()
        {
            while (true)
            {
                var frontier = await NetworkHelper.ReceiveUintArrayAsync(stream);
                if (frontier == null) break;

                var globalVisited = await NetworkHelper.ReceiveByteArrayAsync(stream);
                visited.OverwriteFromByteArray(globalVisited);

                var nextFrontier = SearchFrontier(frontier);

                var data = nextFrontier.ToReadOnlyMemory();
                await NetworkHelper.SendDataAsync(stream, data);
                await NetworkHelper.FlushStreamAsync(stream);
            }
        }

        private List<uint> SearchFrontier(List<uint> frontier)
        {
            List<uint> nextFrontier = new List<uint>();

            foreach (var node in frontier)
            {
                if (!partialGraph.TryGetValue(node, out var neighbors)) continue;

                foreach (var neighbor in neighbors)
                {
                    if (!visited.Get(neighbor))
                    {
                        visited.Set(neighbor);
                        nextFrontier.Add(neighbor);
                    }
                }
            }

            return nextFrontier;
        }
    }
}
