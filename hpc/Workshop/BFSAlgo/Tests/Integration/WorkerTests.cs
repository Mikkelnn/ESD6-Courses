using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Integration
{
    public class WorkerTests
    {
        [Fact]
        public async Task Worker_Start_FullIntegrationTest()
        {
            // Arrange
            var assignedNodes = new List<uint> { 0, 1 };
            var fullGraph = new List<uint>[]
            {
                [1],
                [0],
                []
            };

            // setup tcp server at any available port
            var listener = new TcpListener(IPAddress.Loopback, port: 0); 
            listener.Start();
            var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

            // initiate worker
            var worker = new Worker(serverEndpoint.Address, serverEndpoint.Port);
            var workerTask = Task.Run(worker.Start);

            // wait for worker connection
            var tcpClient = await listener.AcceptTcpClientAsync();
            var networkStream = new NetworkStreamWrapper(tcpClient);

            // Act
            // 0. Send partial graph
            await NetworkHelper.SendGraphPartitionAsync(networkStream, assignedNodes, fullGraph);
            // 1. Send a frontier containing node 0
            await NetworkHelper.SendDataAsync(networkStream, new List<uint> { 0 }.ToReadOnlyMemory());
            // 2. Send empty visited bitmap
            var visitedBitMap = new Bitmap(fullGraph.Length);
            await NetworkHelper.SendDataAsync(networkStream, visitedBitMap.AsReadOnlyMemory);
            await networkStream.FlushAsync();

            // 3. Receive the new frontier
            var receivedFrontier = await NetworkHelper.ReceiveUintArrayAsync(networkStream);

            // 4. Send termination signal
            await NetworkHelper.SendDataAsync(networkStream, null);
            await networkStream.FlushAsync();

            // Wait for the worker to terminate
            await workerTask;

            // Assert
            Assert.Single(receivedFrontier);
            Assert.Equal(1U, receivedFrontier[0]);
        }
    }
}
