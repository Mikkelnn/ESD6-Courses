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

            int port = 5000;
            var worker = new Worker(0, assignedNodes, fullGraph, port);

            var workerTask = Task.Run(worker.Start);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            var networkStream = new NetworkStreamWrapper(client.Client);

            // Act
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
