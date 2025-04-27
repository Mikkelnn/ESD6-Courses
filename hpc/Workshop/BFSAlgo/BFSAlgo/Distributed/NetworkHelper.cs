using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BFSAlgo.Distributed
{
    public interface INetworkStream
    {
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);
        ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
        ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
        Task FlushAsync();
        void Close();
    }

    public class NetworkStreamWrapper(Socket socket) : NetworkStream(socket, true), INetworkStream
    {
        private TcpClient tcpClient;

        public static async Task<NetworkStreamWrapper> GetCoordinatorInstance(IPAddress address, int port)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(address, port);

            //tcpClient.Connect(address, port);

            var instance = new NetworkStreamWrapper(tcpClient.Client);
            instance.tcpClient = tcpClient;
            return instance;
        }

        public static async Task<NetworkStreamWrapper> GetWorkerInstanceAsync(IPAddress address, int port)
        {
            var listener = new TcpListener(address, port);
            listener.Start();

            var tcpClient = await listener.AcceptTcpClientAsync();
            var instance = new NetworkStreamWrapper(tcpClient.Client);
            instance.tcpClient = tcpClient;
            return instance;
        }

        public override void Close()
        {
            base.Close();
            tcpClient?.Close();
        }
    }

    public static class NetworkHelper
    {
        public static ReadOnlyMemory<byte> ToReadOnlyMemory(this List<uint> list)
        {
            var uintArray = list.ToArray();                      // allocate once
            var byteSpan = MemoryMarshal.Cast<uint, byte>(uintArray);
            var byteFrontier = new ReadOnlyMemory<byte>(byteSpan.ToArray()); // Make ReadOnlyMemory<byte> from byteSpan
            return byteFrontier;
        }

        public static async Task FlushStreamAsync(INetworkStream stream) => await stream.FlushAsync();

        public static async Task SendDataAsync(INetworkStream stream, ReadOnlyMemory<byte>? data)
        {
            if (data == null)
            {
                var lenBytes = BitConverter.GetBytes(-1);
                await stream.WriteAsync(lenBytes, 0, 4);  // Send termination signal
                return;
            }

            // Send length of data first so we know the amount to receive at the other end
            var lengthBytes = BitConverter.GetBytes(data.Value.Length);
            await stream.WriteAsync(lengthBytes, 0, 4);
            await stream.WriteAsync(data.Value);
        }

        public static async Task SendGraphPartitionAsync(INetworkStream stream, List<uint> ownedNodes, List<uint>[] fullGraph)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(fullGraph.Length);
            writer.Write(ownedNodes.Count);

            foreach (var node in ownedNodes)
            {
                var neighbors = fullGraph[node];
                writer.Write(node);
                writer.Write(neighbors.Count);
                foreach (var neighbor in neighbors)
                    writer.Write(neighbor);
            }

            var data = ms.ToArray();
            await stream.WriteAsync(BitConverter.GetBytes(data.Length));
            await stream.WriteAsync(data);
        }

        public static async Task<uint[]> ReceiveUintArrayAsync(INetworkStream stream, Stopwatch sw = null)
        {
            // Read 4 bytes for the length first
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes);
            if (totalLength == -1) return null; // Termination signal

            sw?.Start();

            var buffer = new byte[totalLength].AsMemory();
            await stream.ReadExactlyAsync(buffer);

            var uintSpan = MemoryMarshal.Cast<byte, uint>(buffer.Span);
            //return uintSpan.ToArray().ToList();
            return uintSpan.ToArray();
        }

        public static async Task<byte[]> ReceiveByteArrayAsync(INetworkStream stream, Stopwatch sw = null)
        {
            // Read 4 bytes for the length first
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes);
            if (totalLength == -1) return null; // Termination signal

            sw?.Start(); // start if not null

            var buffer = new byte[totalLength];
            await stream.ReadExactlyAsync(buffer.AsMemory());
            return buffer;
        }

        public static async Task<(Dictionary<uint, uint[]>, int)> ReceiveGraphPartitionAsync(INetworkStream stream)
        {
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes);

            var buffer = new byte[totalLength];
            await stream.ReadExactlyAsync(buffer);

            var dict = new Dictionary<uint, uint[]>();
            using var ms = new MemoryStream(buffer);
            using var reader = new BinaryReader(ms);

            int totalNodeCount = reader.ReadInt32();
            int nodeCount = reader.ReadInt32();
            for (int i = 0; i < nodeCount; i++)
            {
                uint nodeId = reader.ReadUInt32();
                int neighborCount = reader.ReadInt32();
                var neighbors = new uint[neighborCount];
                for (int j = 0; j < neighborCount; j++)
                    neighbors[j] = reader.ReadUInt32();

                dict[nodeId] = neighbors;
            }

            return (dict, totalNodeCount);
        }
    }
}
