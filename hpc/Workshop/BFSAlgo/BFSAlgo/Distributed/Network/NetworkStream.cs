using System.Net;
using System.Net.Sockets;

namespace BFSAlgo.Distributed.Network
{
    public interface INetworkStream
    {
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
        ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
        ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
        Task FlushAsync();
        void Close();
    }
   
    public class NetworkStreamWrapper : NetworkStream, INetworkStream
    {
        private TcpClient tcpClient;

        public NetworkStreamWrapper(TcpClient tcpClient) : base(tcpClient.Client, true)
        {
            this.tcpClient = tcpClient;
        }

        /// <summary>
        /// Connect to a coordinator at <paramref name="address"/> : <paramref name="port"/>
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static async Task<NetworkStreamWrapper> GetWorkerInstanceAsync(IPAddress address, int port)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(address, port);

            var instance = new NetworkStreamWrapper(tcpClient);
            instance.tcpClient = tcpClient;
            return instance;
        }

        public override void Close()
        {
            base.Close();
            tcpClient.Close();
        }
    }

    public interface INetworkStreamFactory
    {
        INetworkStream Create(TcpClient client);
    }

    public class NetworkStreamFactory : INetworkStreamFactory
    {
        public INetworkStream Create(TcpClient client) => new NetworkStreamWrapper(client);
    }
}
