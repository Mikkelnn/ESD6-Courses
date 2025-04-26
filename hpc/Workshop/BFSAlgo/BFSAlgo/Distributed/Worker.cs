using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace BFSAlgo.Distributed
{
    public class Worker
    {
        private readonly int id;
        private readonly Bitmap assignedNodes;
        private readonly Bitmap visited;
        private readonly List<uint>[] fullGraph;
        private TcpClient client;
        private NetworkStream stream;
        private readonly int port;

        private Stopwatch sw_ReceiveUintArray = new();
        private Stopwatch sw_frontierSearch = new(); 
        private Stopwatch sw_SendUintArray = new();


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

        public async Task Start()
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Server.NoDelay = true;
            listener.Start();
            client = await listener.AcceptTcpClientAsync();
            stream = client.GetStream();

            while (true)
            {
                //sw_ReceiveUintArray.Start();
                var frontier = await NetworkHelper.ReceiveUintArrayAsync(stream, sw_ReceiveUintArray);
                sw_ReceiveUintArray.Stop();

                if (frontier == null) break;

                var nextFrontier = new List<uint>();

                sw_frontierSearch.Start();
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
                sw_frontierSearch.Stop();

                Console.WriteLine($"nodeId: {id}; visited: {nextFrontier.Count}");

                sw_SendUintArray.Start();
                await NetworkHelper.SendUintArrayAsync(stream, nextFrontier);
                sw_SendUintArray.Stop();
            }

            stream.Close();
            client.Close();


            Console.WriteLine($"nodeId: {id}; ReceiveUintArray: {sw_ReceiveUintArray.ElapsedMilliseconds} ms, frontierSearch: {sw_frontierSearch.ElapsedMilliseconds} ms, SendUintArray: {sw_SendUintArray.ElapsedMilliseconds} ms");
        }
    }
}
