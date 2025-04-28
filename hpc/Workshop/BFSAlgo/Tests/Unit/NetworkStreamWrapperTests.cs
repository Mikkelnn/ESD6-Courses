using BFSAlgo.Distributed;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Tests.Unit
{
    public class NetworkStreamWrapperTests
    {
        // Test for creating a NetworkStreamWrapper instance for a worker.
        [Fact]
        public async Task GetWorkerInstanceAsync_CreatesNetworkStreamWrapperSuccessfully()
        {
            // Arrange: Start a TCP listener at any available port to simulate a server.
            var listener = new TcpListener(IPAddress.Loopback, port: 0);
            listener.Start();
            var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

            // Act: Create the NetworkStreamWrapper for the worker async (server).
            var networkStreamWrapper = await NetworkStreamWrapper.GetWorkerInstanceAsync(serverEndpoint.Address, serverEndpoint.Port);

            // Wait for worker connection
            var stream = (await listener.AcceptTcpClientAsync()).GetStream();

            // Send some data to the worker
            byte[] message = Encoding.UTF8.GetBytes("Hello, Worker!");
            await stream.WriteAsync(message, 0, message.Length);


            // Read the message sent from the client
            byte[] buffer = new byte[1024];
            int bytesRead = await networkStreamWrapper.ReadAsync(buffer, 0, buffer.Length);
            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Assert: Check that the message was successfully received by the server
            Assert.Equal("Hello, Worker!", receivedMessage);

            // Cleanup: Stop the listener and close resources
            networkStreamWrapper.Close();
        }

        // Test for closing the NetworkStreamWrapper and verifying TcpClient is closed.
        [Fact]
        public void Close_ClosesTcpClientAndNetworkStream()
        {
            // Arrange: Start a TCP listener to simulate a server.
            var listener = new TcpListener(IPAddress.Loopback, port: 0);
            listener.Start();
            var serverEndpoint = (IPEndPoint)listener.LocalEndpoint;

            // Act: Create a NetworkStreamWrapper instance
            var client = new TcpClient();
            client.Connect(serverEndpoint);
            var networkStreamWrapper = new NetworkStreamWrapper(client);

            // Act: Close the stream and verify the TcpClient is closed
            networkStreamWrapper.Close();

            // Assert: Verify that TcpClient Close is invoked
            Assert.Throws<ObjectDisposedException>(() => networkStreamWrapper.ReadByte());  // After Close, the stream should throw this exception

            // Cleanup: Stop the listener
            listener.Stop();
        }
    }
}
