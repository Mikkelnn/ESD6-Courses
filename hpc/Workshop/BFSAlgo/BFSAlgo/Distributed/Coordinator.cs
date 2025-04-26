using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo.Distributed
{
    public class Coordinator
    {
        private readonly List<uint>[] graph;
        private readonly uint startNode;
        private readonly int numWorkers;
        private readonly bool evenPartitioning;
        private readonly int basePort = 9000;
        private List<uint>[] partitioned;

        public Coordinator(List<uint>[] graph, uint startNode, int numWorkers, bool evenPartitioning)
        {
            this.graph = graph;
            this.startNode = startNode;
            this.numWorkers = numWorkers;
            this.evenPartitioning = evenPartitioning;
        }

        public async Task Run()
        {
            //Stopwatch sw = Stopwatch.StartNew();
            partitioned = GraphPartitioner.Partition(graph, numWorkers, evenPartitioning);
            //sw.Stop();
            //Console.WriteLine($"coordinator; partion time: {sw.ElapsedMilliseconds} ms");

            var workerTasks = new Task[numWorkers];

            for (int i = 0; i < numWorkers; i++)
            {
                int port = basePort + i;
                int id = i;
                workerTasks[i] = Task.Run(async () =>
                {
                    var worker = new Worker(id, partitioned[id], graph, port);
                    await worker.Start();
                });
            }

            //await Task.Delay(1000);

            // Connect to workers
            var clients = new TcpClient[numWorkers];
            var streams = new NetworkStream[numWorkers];
            for (int i = 0; i < numWorkers; i++)
            {
                clients[i] = new TcpClient("localhost", basePort + i);
                clients[i].Client.NoDelay = true;
                streams[i] = clients[i].GetStream();
            }

            var frontier = new List<uint> { startNode };
            var visitedGlobal = new Bitmap(graph.Length);
            visitedGlobal.Set(startNode);


            Stopwatch sw_visitedGlobal = new();
            Stopwatch sw_sendFrontiers = new();
            Stopwatch sw_recieveFrontiers = new();

            int frontierIndex = 0;
            while (frontier.Count > 0)
            {
                Console.WriteLine($"coordinator; frontierIndex: {frontierIndex}, size: {frontier.Count}");
                frontierIndex++;

                // Broadcast
                sw_sendFrontiers.Start();
                var sendTasks = streams.Select(stream => NetworkHelper.SendUintArrayAsync(stream, frontier));                                
                await Task.WhenAll(sendTasks);  // Wait for all sends to complete
                sw_sendFrontiers.Stop();

                var newFrontier = new List<uint>();

                sw_recieveFrontiers.Start();
                var receiveTasks = streams.Select(s => NetworkHelper.ReceiveUintArrayAsync(s));
                var results = await Task.WhenAll(receiveTasks);  // Wait for all receives to complete
                sw_recieveFrontiers.Stop();

                for (int i = 0; i < numWorkers; i++)
                {
                    //var partial = NetworkHelper.ReceiveUintArrayAsync(streams[i]);
                    sw_visitedGlobal.Start();
                    foreach (var node in results[i])
                    {
                        if (!visitedGlobal.Get(node))
                        {
                            visitedGlobal.Set(node);
                            newFrontier.Add(node);                            
                        }
                    }
                    sw_visitedGlobal.Stop();
                }

                frontier = newFrontier;
            }

            Console.WriteLine($"coordinator; sendFrontiers: {sw_sendFrontiers.ElapsedMilliseconds} ms");
            Console.WriteLine($"coordinator; recieveFrontiers: {sw_recieveFrontiers.ElapsedMilliseconds} ms");
            Console.WriteLine($"coordinator; visitedGlobal time: {sw_visitedGlobal.ElapsedMilliseconds} ms");

            // Tell workers to stop
            var sendTasksStop = streams.Select(stream => NetworkHelper.SendUintArrayAsync(stream, null));
            await Task.WhenAll(sendTasksStop);  // Wait for all sends to complete

            for (int i = 0; i < numWorkers; i++)
            {
                //NetworkHelper.SendUintArray(streams[i], null);
                streams[i].Close();
                clients[i].Close();
            }

            Task.WaitAll(workerTasks);
        }
    }

}
