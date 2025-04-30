using BFSAlgo.Distributed.Network;
using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Tests.Mocks;
using Moq;

namespace Tests.Unit
{
    public class CoordinatorTests
    {
        [Fact]
        public async Task Coordinator_RegistersWorker_OnClientConnection()
        {
            // Arrange
            var mockListener = new Mock<ITcpListener>();
            var mockNetworkStream = new Mock<INetworkStream>();
            var mockFactory = new Mock<INetworkStreamFactory>();
            var mockHelper = new Mock<INetworkHelper>();

            var fakeTcpClient = new TcpClient(); // can be mocked if needed

            // Setup listener to return one client, then block (simulate one worker)
            var acceptCallCount = 0;
            mockListener.Setup(l => l.Start());
            mockListener.Setup(l => l.LocalEndpoint).Returns(new IPEndPoint(IPAddress.Loopback, 1234));
            mockListener.Setup(l => l.AcceptTcpClientAsync()).Returns(() =>
            {
                if (acceptCallCount++ == 0)
                    return Task.FromResult(fakeTcpClient);
                return new TaskCompletionSource<TcpClient>().Task; // block forever
            });

            // Return a mock stream when factory is called
            mockFactory.Setup(f => f.Create(fakeTcpClient)).Returns(mockNetworkStream.Object);

            var coordinator = new Coordinator(mockListener.Object, mockFactory.Object, mockHelper.Object);

            // Act
            var workerTask = coordinator.StartAsync();

            // Wait until worker connects
            await coordinator.WaitForWorkerCountAsync(1);

            // Assert
            Assert.Equal(1, coordinator.ConnectedWorkers);
            mockFactory.Verify(f => f.Create(fakeTcpClient), Times.Once);
        }

        [Fact]
        public async Task Coordinator_RunAsync_SendsAndReceivesCorrectly()
        {
            // Arrange
            var mockHelper = new Mock<INetworkHelper>();

            var stream1 = new NetworkStreamMock();
            var stream2 = new NetworkStreamMock();
            var streams = new[] { stream1, stream2 };

            var mockFactory = new Mock<INetworkStreamFactory>();
            var mockListener = new Mock<ITcpListener>();

            mockFactory.SetupSequence(f => f.Create(It.IsAny<TcpClient>()))
                       .Returns(stream1)
                       .Returns(stream2);

            var client1 = new TcpClient();
            var client2 = new TcpClient();
            var clients = new Queue<TcpClient>(new[] { client1, client2 });

            mockListener.Setup(l => l.AcceptTcpClientAsync()).ReturnsAsync(() => clients.Dequeue());
            mockListener.Setup(l => l.LocalEndpoint).Returns(new IPEndPoint(IPAddress.Loopback, 0));
            mockListener.Setup(l => l.Start());

            // Simulated graph: 0 → 1 → 2, linear
            var graph = new List<uint>[]
            {
                new() { 1 },
                new() { 2 },
                new() { } // end
            };

            // Simulate partition response — just echoing back "next frontier"
            mockHelper.Setup(h => h.SendGraphPartitionAsync(It.IsAny<INetworkStream>(), It.IsAny<List<uint>>(), It.IsAny<List<uint>[]>()))
                      .Returns(Task.CompletedTask);

            mockHelper.Setup(h => h.SendByteArrayAsync(It.IsAny<INetworkStream>(), It.IsAny<ReadOnlyMemory<byte>>()))
                      .Returns(Task.CompletedTask);

            mockHelper.Setup(h => h.SendByteArrayAsync(It.IsAny<INetworkStream>(), null))
                      .Returns(Task.CompletedTask);

            mockHelper.Setup(h => h.FlushStreamAsync(It.IsAny<INetworkStream>()))
                      .Returns(Task.CompletedTask);

            // Setup fake responses: each "worker" returns one node
            mockHelper.SetupSequence(h => h.ReceiveUintArrayAsync(It.IsAny<INetworkStream>()))
                      .ReturnsAsync(new uint[] { 1 })  // round 1, worker 1
                      .ReturnsAsync(new uint[0])       // round 1, worker 2 empty
                      .ReturnsAsync(new uint[0])       // round 2, worker 1 empty
                      .ReturnsAsync(new uint[] { 2 })  // round 2, worker 2
                      .ReturnsAsync(new uint[0])       // round 3, worker 1 empty as no nodes in last
                      .ReturnsAsync(new uint[0]);      // round 3, worker 2 empty



            var coordinator = new Coordinator(mockListener.Object, mockFactory.Object, mockHelper.Object);
            var runTask = coordinator.StartAsync();
            await coordinator.WaitForWorkerCountAsync(2);

            // Act
            var visited = await coordinator.RunAsync(graph, 0);

            // Assert
            Assert.True(visited.Get(0));
            Assert.True(visited.Get(1));
            Assert.True(visited.Get(2));

            // Termination
            mockHelper.Verify(h => h.SendByteArrayAsync(It.IsAny<INetworkStream>(), null), Times.Exactly(2));
            mockHelper.Verify(h => h.FlushStreamAsync(It.IsAny<INetworkStream>()), Times.AtLeast(2));
        }

    }
}
