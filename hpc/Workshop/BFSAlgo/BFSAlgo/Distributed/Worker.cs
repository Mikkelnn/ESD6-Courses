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
        private readonly Bitmap assignedNodes;
        private readonly Bitmap visited;
        private readonly List<uint>[] fullGraph;
        private readonly int port;

        private TcpClient client;
        private INetworkStream stream;


        //private Stopwatch sw_ReceiveUintArray = new();
        //private Stopwatch sw_frontierSearch = new(); 
        //private Stopwatch sw_SendUintArray = new();
        //private Stopwatch sw_GetVisited = new();


        public Worker(int id, List<uint> assignedNodes, List<uint>[] fullGraph, int port)
        {
            this.id = id;
            this.assignedNodes = new Bitmap(fullGraph.Length);
            this.visited = new Bitmap(fullGraph.Length);
            this.fullGraph = fullGraph;
            this.port = port;

            for (int i = 0; i < assignedNodes.Count; i++)
                this.assignedNodes.Set(assignedNodes[i]);
        }

        // Constructor for DI/testing
        public Worker(int id, List<uint> assignedNodes, List<uint>[] fullGraph, int port, INetworkStream stream)
            : this(id, assignedNodes, fullGraph, port)
        {
            this.stream = stream;
        }

        public async Task Start()
        {
            if (stream == null)
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                client = await listener.AcceptTcpClientAsync();
                stream = new NetworkStreamWrapper(client.Client);
            }

            await RunMainLoop();

            stream?.Close();
            client?.Close();
        }

        private async Task RunMainLoop()
        {
            while (true)
            {
                //sw_ReceiveUintArray.Start();
                var frontier = await NetworkHelper.ReceiveUintArrayAsync(stream);
                //sw_ReceiveUintArray.Stop();
                if (frontier == null) break;

                var globalVisited = await NetworkHelper.ReceiveByteArrayAsync(stream);
                visited.OverwriteFromByteArray(globalVisited);
                //sw_GetVisited.Stop();

                //sw_frontierSearch.Start();
                var nextFrontier = SearchFrontier(frontier);
                //sw_frontierSearch.Stop();

                //Console.WriteLine($"nodeId: {id}; visited: {nextFrontier.Count}");

                //sw_SendUintArray.Start();
                var data = nextFrontier.ToReadOnlyMemory();
                await NetworkHelper.SendDataAsync(stream, data);
                await NetworkHelper.FlushStreamAsync(stream);
                //NetworkHelper.SendUintArray(stream, nextFrontier);
                //sw_SendUintArray.Stop();
            }

            //Console.WriteLine($"nodeId: {id}; " +
            //    $"ReceiveUintArray: {sw_ReceiveUintArray.ElapsedMilliseconds} ms, " +
            //    $"frontierSearch: {sw_frontierSearch.ElapsedMilliseconds} ms, " +
            //    $"SendUintArray: {sw_SendUintArray.ElapsedMilliseconds} ms " +
            //    $"GetVisited: {sw_GetVisited.ElapsedMilliseconds} ms");
        }

        private List<uint> SearchFrontier(List<uint> frontier)
        {
            List<uint> nextFrontier = new List<uint>();

            foreach (var node in frontier)
            {
                if (!assignedNodes.Get(node)) continue;

                foreach (var neighbor in fullGraph[node])
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
