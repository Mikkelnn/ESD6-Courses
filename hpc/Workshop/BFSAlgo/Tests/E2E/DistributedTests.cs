using BFSAlgo.Distributed.Network;
using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tests.E2E
{
    public class DistributedTests
    {
        [Fact]
        public async Task Coordinator_And_Worker_EndToEnd_WalksGraphCorrectly()
        {
            // Arrange: simple graph 0 → 1 → 2
            var graph = new List<uint>[]
            {
                new() { 1 },   // 0
                new() { 2 },   // 1
                new()          // 2
            };
            uint startNode = 0;

            // Coordinator dependencies (real)
            var coordinator = new Coordinator(IPAddress.Loopback, 0);
            _ = coordinator.StartAsync();

            var endpoint = coordinator.ListeningOn;

            // Start real worker
            var worker = new Worker(endpoint.Address, endpoint.Port);
            var workerTask = Task.Run(worker.Start);

            // Wait for coordinator to detect connection
            await coordinator.WaitForWorkerCountAsync(1);

            // Act: run Coordinator
            var visited = await coordinator.RunAsync(graph, startNode);

            // Assert: all nodes should be visited
            Assert.True(visited.Get(0));
            Assert.True(visited.Get(1));
            Assert.True(visited.Get(2));

            // Wait for worker to finish
            await workerTask;
        }

        [Fact]
        public async Task Coordinator_EndToEnd_MultipleRealWorkers_VisitsAllNodes()
        {
            // Arrange
            var graph = new List<uint>[]
            {
                new() { 1 },
                new() { 2 },
                new() { 3 },
                new()
            };
            uint startNode = 0;
            int workerCount = 3;

            var coordinator = new Coordinator(IPAddress.Loopback, 0);
            _ = coordinator.StartAsync(); // fire-and-forget
            var endpoint = coordinator.ListeningOn;

            // Start real workers
            var workerTasks = new List<Task>();
            for (int i = 0; i < workerCount; i++)
            {
                var worker = new Worker(endpoint.Address, endpoint.Port);
                workerTasks.Add(Task.Run(worker.Start));
            }

            await coordinator.WaitForWorkerCountAsync(workerCount);

            // Act
            var result = await Task.WhenAny(coordinator.RunAsync(graph, startNode), Task.Delay(3000));
            Assert.True(result is Task<Bitmap>, "Coordinator did not finish in time");

            // Assert
            var visited = (await (Task<Bitmap>)result);
            for (uint i = 0; i < graph.Length; i++)
                Assert.True(visited.Get(i), $"Node {i} not visited");

            await Task.WhenAll(workerTasks);
        }

    }
}
