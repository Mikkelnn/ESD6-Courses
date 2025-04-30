using BFSAlgo.Distributed.Network;
using BFSAlgo.Distributed;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Tests.Mocks;
using System.Net;
using System.Threading;

namespace Tests.Integration
{
    public class CoordinatorTests
    {
        [Fact]
        public async Task Coordinator_RunAsync_WithMockStream_VisitsAllReachableNodes()
        {
            // Arrange
            var graph = new List<uint>[]
            {
                new() { 1 },   // Node 0 -> 1
                new() { 2 },   // Node 1 -> 2
                new()          // Node 2
            };
            var startNode = 0U;

            // Setup mock stream that simulates a worker
            var stream = new NetworkStreamMock();

            // 1. Add worker response: returns node 1 after first frontier (node 0)
            stream.AddDataToRead(BitConverter.GetBytes(1U)); // Will be read as the frontier result
            stream.AddDataToRead(BitConverter.GetBytes(0));  // Fake length prefix for next read (termination)

            var streamFactoryMock = new Mock<INetworkStreamFactory>();
            streamFactoryMock.Setup(f => f.Create(It.IsAny<TcpClient>())).Returns(stream);

            var networkHelperMock = new Mock<INetworkHelper>();

            // Fake ReceiveUintArrayAsync will read what we put in mock
            networkHelperMock.Setup(h => h.ReceiveUintArrayAsync(stream)).ReturnsAsync(() => new uint[] { 1U });

            networkHelperMock.Setup(h => h.SendGraphPartitionAsync(
                stream,
                It.IsAny<List<uint>>(),
                It.IsAny<List<uint>[]>()))
                .Returns(Task.CompletedTask);

            networkHelperMock.Setup(h => h.SendByteArrayAsync(stream, It.IsAny<ReadOnlyMemory<byte>>()))
                .Returns(Task.CompletedTask);

            networkHelperMock.Setup(h => h.FlushStreamAsync(stream))
                .Returns(Task.CompletedTask);

            var acceptCallCount = 0;
            var listenerMock = new Mock<ITcpListener>();
            listenerMock.Setup(l => l.Start());
            listenerMock.Setup(l => l.AcceptTcpClientAsync())
                .Returns(() =>
                {
                    if (acceptCallCount++ == 0)
                        return Task.FromResult(new TcpClient());
                    // Simulate blocking forever after first
                    var neverCompletingTask = new TaskCompletionSource<TcpClient>();
                    return neverCompletingTask.Task;
                });

            var coordinator = new Coordinator(listenerMock.Object, streamFactoryMock.Object, networkHelperMock.Object);

            // Connect one worker
            var waitTask = coordinator.WaitForWorkerCountAsync(1);
            _ = Task.Run(() => coordinator.StartAsync()); // run accept loop
            await waitTask;

            // Act
            var visited = await coordinator.RunAsync(graph, startNode);

            // Assert
            Assert.True(visited.Get(0));
            Assert.True(visited.Get(1));
            Assert.False(visited.Get(2)); // not reached because mock only returned 1
        }

        [Fact]
        public async Task Coordinator_RunAsync_MultipleIterations_VisitsAllReachableNodes()
        {
            // Arrange
            var graph = new List<uint>[]
            {
                new() { 1 },   // 0 -> 1
                new() { 2 },   // 1 -> 2
                new()          // 2
            };
            var startNode = 0U;

            var stream = new NetworkStreamMock();
            var streamFactoryMock = new Mock<INetworkStreamFactory>();
            streamFactoryMock.Setup(f => f.Create(It.IsAny<TcpClient>())).Returns(stream);

            var acceptCallCount = 0;
            var listenerMock = new Mock<ITcpListener>();
            listenerMock.Setup(l => l.Start());
            listenerMock.Setup(l => l.AcceptTcpClientAsync()).Returns(() =>
            {
                if (acceptCallCount++ == 0)
                    return Task.FromResult(new TcpClient());
                // Simulate blocking forever after first
                var neverCompletingTask = new TaskCompletionSource<TcpClient>();
                return neverCompletingTask.Task;
            });

            // We'll simulate two BFS iterations:
            // First iteration returns node 1
            // Second iteration returns node 2
            var networkHelperMock = new Mock<INetworkHelper>();
            networkHelperMock.Setup(h => h.SendGraphPartitionAsync(stream, It.IsAny<List<uint>>(), It.IsAny<List<uint>[]>()))
                .Returns(Task.CompletedTask);

            networkHelperMock.Setup(h => h.SendByteArrayAsync(stream, It.IsAny<ReadOnlyMemory<byte>>()))
                .Returns(Task.CompletedTask);

            networkHelperMock.Setup(h => h.FlushStreamAsync(stream))
                .Returns(Task.CompletedTask);

            networkHelperMock.SetupSequence(h => h.ReceiveUintArrayAsync(stream))
                .ReturnsAsync(new uint[] { 1U })  // After first frontier
                .ReturnsAsync(new uint[] { 2U })  // After second frontier
                .ReturnsAsync(Array.Empty<uint>()); // Final: no more frontier nodes

            var coordinator = new Coordinator(listenerMock.Object, streamFactoryMock.Object, networkHelperMock.Object);

            // Act
            var waitForConnect = coordinator.WaitForWorkerCountAsync(1);
            _ = Task.Run(coordinator.StartAsync);
            await waitForConnect;

            var visited = await coordinator.RunAsync(graph, startNode);

            // Assert
            Assert.True(visited.Get(0)); // Start node
            Assert.True(visited.Get(1)); // Discovered in first iteration
            Assert.True(visited.Get(2)); // Discovered in second iteration
        }

        [Fact]
        public async Task Coordinator_RunAsync_Integration_SimulatedWorker_VisitsAllReachableNodes()
        {
            var timeOut = TimeSpan.FromSeconds(1); // If something fails stop after a second

            // Arrange: create a 3-node graph: 0 → 1 → 2
            var graph = new List<uint>[]
            {
                new() { 1 },   // 0
                new() { 2 },   // 1
                new()          // 2
            };
            uint startNode = 0;

            var listener = new TcpListenerWrapper(IPAddress.Loopback, 0);
            listener.Start();
            var endpoint = (IPEndPoint)listener.LocalEndpoint;

            // Coordinator setup
            var networkHelper = new NetworkHelper(); // real
            var streamFactory = new NetworkStreamFactory(); // real
            var coordinator = new Coordinator(listener, streamFactory, networkHelper);

            // Simulated worker task
            Exception? workerException = null;
            var workerTask = Task.Run(async () =>
            {
                try
                {
                    var stream = await NetworkStreamWrapper.GetWorkerInstanceAsync(endpoint.Address, endpoint.Port);

                    // 1. Receive partial graph (not validated in this test)
                    _ = await networkHelper.ReceiveGraphPartitionAsync(stream);

                    // 2. Receive frontier + visited bitmap → return node 1
                    var frontier1 = await networkHelper.ReceiveUintArrayAsync(stream);
                    var visited1 = await networkHelper.ReceiveUintArrayAsync(stream);
                    Assert.Contains(0U, frontier1);
                    await networkHelper.SendByteArrayAsync(stream, new List<uint> { 1 }.ToReadOnlyMemory()); // next frontier
                    await networkHelper.FlushStreamAsync(stream);

                    // 3. Receive frontier + visited bitmap → return node 2
                    var frontier2 = await networkHelper.ReceiveUintArrayAsync(stream);
                    var visited2 = await networkHelper.ReceiveUintArrayAsync(stream);
                    Assert.Contains(1U, frontier2);
                    await networkHelper.SendByteArrayAsync(stream, new List<uint> { 2 }.ToReadOnlyMemory());
                    await networkHelper.FlushStreamAsync(stream);

                    // 4. Receive frontier + visited bitmap → return none
                    var frontier3 = await networkHelper.ReceiveUintArrayAsync(stream);
                    var visited3 = await networkHelper.ReceiveUintArrayAsync(stream);
                    Assert.Contains(2U, frontier3);
                    await networkHelper.SendByteArrayAsync(stream, new List<uint> { }.ToReadOnlyMemory());
                    await networkHelper.FlushStreamAsync(stream);

                    // 4. Termination signal
                    var terminator = await networkHelper.ReceiveUintArrayAsync(stream);
                    Assert.Null(terminator); // End of graph signal
                }
                catch (Exception ex)
                {
                    workerException = ex;
                    throw;
                }
            });

            var waitForConnect = coordinator.WaitForWorkerCountAsync(1);
            _ = Task.Run(coordinator.StartAsync);
            await waitForConnect;

            // Act
            var result = await coordinator.RunAsync(graph, startNode).WaitAsync(timeOut);

            if (result == null)
                // Fail test if background worker had exceptions
                if (workerException is not null)
                    throw new Xunit.Sdk.XunitException("Worker task failed", workerException);

            // Assert
            Assert.True(result.Get(0));
            Assert.True(result.Get(1));
            Assert.True(result.Get(2));

            // Cleanup
            await workerTask;

            
        }

    }
}
