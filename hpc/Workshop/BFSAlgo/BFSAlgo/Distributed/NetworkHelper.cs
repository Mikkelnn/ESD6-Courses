using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
            long totalNeighbors = 0;
            foreach (var node in ownedNodes)
                totalNeighbors += fullGraph[node].Count;

            // 4 bytes per uint
            int esimatedSize = sizeof(int) * 3 + (ownedNodes.Count * (sizeof(uint) * 2)) + ((int)totalNeighbors * sizeof(uint));


            byte[] buffer = ArrayPool<byte>.Shared.Rent(esimatedSize);
            int offset = 0;

            // Helper to write uint
            void WriteUInt(uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, 4), value);
                offset += 4;
            }

            WriteUInt((uint)fullGraph.Length);
            WriteUInt((uint)ownedNodes.Count);
            foreach (var node in ownedNodes)
            {
                var neighbors = fullGraph[node];
                WriteUInt(node);
                WriteUInt((uint)neighbors.Count);
                foreach (var neighbor in neighbors)
                    WriteUInt(neighbor);
            }

            var data = buffer.AsMemory(0, offset);
            await stream.WriteAsync(BitConverter.GetBytes(data.Length));
            await stream.WriteAsync(data);
            ArrayPool<byte>.Shared.Return(buffer);
        }

        public static async Task<uint[]> ReceiveUintArrayAsync(INetworkStream stream)
        {
            // Read 4 bytes for the length first
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes);
            if (totalLength == -1) return null; // Termination signal

            var buffer = new byte[totalLength].AsMemory();
            await stream.ReadExactlyAsync(buffer);

            var uintSpan = MemoryMarshal.Cast<byte, uint>(buffer.Span);
            return uintSpan.ToArray();
        }

        public static async Task<byte[]> ReceiveByteArrayAsync(INetworkStream stream)
        {
            // Read 4 bytes for the length first
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes);
            if (totalLength == -1) return null; // Termination signal

            var buffer = new byte[totalLength];
            await stream.ReadExactlyAsync(buffer.AsMemory());
            return buffer;
        }

        //public static async Task<(Dictionary<uint, uint[]>, int)> ReceiveGraphPartitionAsync(INetworkStream stream)
        public static async Task<(uint[][], int)> ReceiveGraphPartitionAsync(INetworkStream stream)
        {
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes);

            //Stopwatch total = Stopwatch.StartNew();

            //Stopwatch alloc = Stopwatch.StartNew();
            var buffer = ArrayPool<byte>.Shared.Rent(totalLength);
            //alloc.Stop();

            //Stopwatch network = Stopwatch.StartNew();
            await stream.ReadExactlyAsync(buffer.AsMemory(0, totalLength));
            //network.Stop();

            //Stopwatch cast = Stopwatch.StartNew();
            var uintData = MemoryMarshal.Cast<byte, uint>(buffer.AsSpan(0, totalLength));
            //cast.Stop();
            
            int offset = 0;

            int totalNodeCount = (int)uintData[offset++];
            int nodeCount = (int)uintData[offset++];

            var dict = new uint[totalNodeCount][];

            Stopwatch parse = Stopwatch.StartNew();
            for (int i = 0; i < nodeCount; i++)
            {
                uint nodeId = uintData[offset++];
                int neighborCount = (int)uintData[offset++];

                var neighbors = new uint[neighborCount];
                uintData.Slice(offset, neighborCount).CopyTo(neighbors);

                dict[nodeId] = neighbors;

                offset += neighborCount;
            }

            //using var ms = new MemoryStream(buffer, 0, totalLength);
            //using var reader = new BinaryReader(ms);

            //int totalNodeCount = reader.ReadInt32();
            //int nodeCount = reader.ReadInt32();
            //for (int i = 0; i < nodeCount; i++)
            //{
            //    uint nodeId = reader.ReadUInt32();
            //    int neighborCount = reader.ReadInt32();
            //    var neighbors = new uint[neighborCount];
            //    for (int j = 0; j < neighborCount; j++)
            //        neighbors[j] = reader.ReadUInt32();

            //    dict[nodeId] = neighbors;
            //}
            //parse.Stop();

            ArrayPool<byte>.Shared.Return(buffer);
            //total.Stop();

            //Console.WriteLine($"Worker total: {total.ElapsedMilliseconds} ms, " /*+
            //    $"Alloc: {alloc.ElapsedMilliseconds}, " +
            //    $"Cast: {cast.ElapsedMilliseconds}, " +
            //    $"Network: {network.ElapsedMilliseconds}, "*/ +
            //    $"Parse: {parse.ElapsedMilliseconds}");

            return (dict, totalNodeCount);
        }
    }
}
