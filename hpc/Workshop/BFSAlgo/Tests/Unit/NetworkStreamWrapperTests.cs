using BFSAlgo.Distributed;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Unit
{
    public class NetworkStreamWrapperTests
    {
        // Get port per paralell test
        private static int concurrrent = 0;
        private static int TestPort => 9100 + (concurrrent++);

        // Test for creating a NetworkStreamWrapper instance for a coordinator.
        [Fact]
        public async Task GetCoordinatorInstance_CreatesNetworkStreamWrapperSuccessfully()
        {
            int testPort = TestPort;

            // Arrange: Start a TCP listener to simulate a server.
            var listener = new TcpListener(IPAddress.Loopback, testPort);
            listener.Start();

            // Accept the connection async on the server side
            var tcpClient = listener.AcceptTcpClientAsync();

            // Act: Create the NetworkStreamWrapper for the coordinator.
            var clientTask = Task.Run(async () =>
            {
                var networkStreamWrapper = await NetworkStreamWrapper.GetCoordinatorInstance(IPAddress.Loopback, testPort);

                // Send some data to the server
                byte[] message = Encoding.UTF8.GetBytes("Hello, From coordinator!");
                await networkStreamWrapper.WriteAsync(message, 0, message.Length);

                // Close the client after sending data
                networkStreamWrapper.Close();
            });

            // Wait for the connection on the server side
            var serverStream = (await tcpClient).GetStream();

            // Read the message sent from the client
            byte[] buffer = new byte[1024];
            int bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length);
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Assert: Check that the message was successfully received by the server
            Assert.Equal("Hello, From coordinator!", receivedMessage);

            // Cleanup: Stop the listener and close resources
            listener.Stop();
            await clientTask;
        }

        // Test for creating a NetworkStreamWrapper instance for a worker.
        [Fact]
        public async Task GetWorkerInstanceAsync_CreatesNetworkStreamWrapperSuccessfully()
        {
            int testPort = TestPort;

            // Act: Create the NetworkStreamWrapper for the worker async (server).
            var networkStreamWrapperTask = NetworkStreamWrapper.GetWorkerInstanceAsync(IPAddress.Loopback, testPort);

            // Simulate client (coordinator) connection in a separate task
            var clientTask = Task.Run(async () =>
            {
                var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, testPort);
                var stream = client.GetStream();

                // Send some data to the server
                byte[] message = Encoding.UTF8.GetBytes("Hello, Worker!");
                await stream.WriteAsync(message, 0, message.Length);

                // Close the client after sending data
                client.Close();
            });

            // Wait for connection
            var networkStreamWrapper = await networkStreamWrapperTask;

            // Read the message sent from the client
            byte[] buffer = new byte[1024];
            int bytesRead = await networkStreamWrapper.ReadAsync(buffer, 0, buffer.Length);
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Assert: Check that the message was successfully received by the server
            Assert.Equal("Hello, Worker!", receivedMessage);

            // Cleanup: Stop the listener and close resources
            networkStreamWrapper.Close();
            await clientTask;
        }

        // Test for closing the NetworkStreamWrapper and verifying TcpClient is closed.
        [Fact]
        public void Close_ClosesTcpClientAndNetworkStream()
        {
            int testPort = TestPort;

            // Arrange: Start a TCP listener to simulate a server.
            var listener = new TcpListener(IPAddress.Loopback, testPort);
            listener.Start();

            // Act: Create a NetworkStreamWrapper instance
            var networkStreamWrapper = new NetworkStreamWrapper(new TcpClient(IPAddress.Loopback.ToString(), testPort).Client);

            // Act: Close the stream and verify the TcpClient is closed
            networkStreamWrapper.Close();

            // Assert: Verify that TcpClient Close is invoked
            Assert.Throws<ObjectDisposedException>(() => networkStreamWrapper.ReadByte());  // After Close, the stream should throw this exception

            // Cleanup: Stop the listener
            listener.Stop();
        }

        [Fact]
        public async Task GetCoordinatorInstance_ConnectToWorkerInstanceAsync()
        {
            int testPort = TestPort;

            // Act: Create the NetworkStreamWrapper for the worker.
            var clientTask = Task.Run(async () =>
            {
                var networkStreamWrapper = await NetworkStreamWrapper.GetWorkerInstanceAsync(IPAddress.Loopback, testPort);

                // Send some data to the server
                byte[] message = Encoding.UTF8.GetBytes("Hello, From worker!");
                await networkStreamWrapper.WriteAsync(message, 0, message.Length);

                // Close the client after sending data
                networkStreamWrapper.Close();
            });

            // create coordinator
            var networkStreamWrapper = await NetworkStreamWrapper.GetCoordinatorInstance(IPAddress.Loopback, testPort);

            // Read the message sent from the worker
            byte[] buffer = new byte[1024];
            int bytesRead = await networkStreamWrapper.ReadAsync(buffer, 0, buffer.Length);
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Assert: Check that the message was successfully received by the coordinator
            Assert.Equal("Hello, From worker!", receivedMessage);

            await clientTask;
        }
    }
}
