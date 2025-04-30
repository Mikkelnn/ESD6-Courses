using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BFSAlgo.Distributed.Network
{
    public interface INetworkHelper
    {
        Task FlushStreamAsync(INetworkStream stream);
        Task SendByteArrayAsync(INetworkStream stream, ReadOnlyMemory<byte>? data);
        Task SendGraphPartitionAsync(INetworkStream stream, List<uint> partition, List<uint>[] fullGraph);
        
        Task<uint[]> ReceiveUintArrayAsync(INetworkStream stream);
        Task<byte[]> ReceiveByteArrayAsync(INetworkStream stream);
        Task<(ArraySegment<uint>[], int)> ReceiveGraphPartitionAsync(INetworkStream stream);
    }

    public class NetworkHelper : INetworkHelper
    {
        public async Task FlushStreamAsync(INetworkStream stream) => await stream.FlushAsync();

        public async Task SendByteArrayAsync(INetworkStream stream, ReadOnlyMemory<byte>? data)
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

        public async Task SendGraphPartitionAsync(INetworkStream stream, List<uint> ownedNodes, List<uint>[] fullGraph)
        {
            long totalNeighbors = 0;
            foreach (var node in ownedNodes)
                totalNeighbors += fullGraph[node].Count;

            // 4 bytes per uint
            int esimatedSize = sizeof(int) * 3 + ownedNodes.Count * sizeof(uint) * 2 + (int)totalNeighbors * sizeof(uint);


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

        public async Task<uint[]> ReceiveUintArrayAsync(INetworkStream stream)
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

        public async Task<byte[]> ReceiveByteArrayAsync(INetworkStream stream)
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

        public async Task<(ArraySegment<uint>[], int)> ReceiveGraphPartitionAsync(INetworkStream stream)
        {
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes);
            int totalLength = BitConverter.ToInt32(lengthBytes);

            //Stopwatch total = Stopwatch.StartNew();

            //Stopwatch alloc = Stopwatch.StartNew();
            var buffer = ArrayPool<byte>.Shared.Rent(totalLength);
            var uintData = new uint[totalLength / sizeof(uint)];
            //alloc.Stop();

            //Stopwatch network = Stopwatch.StartNew();
            await stream.ReadExactlyAsync(buffer.AsMemory(0, totalLength));
            //network.Stop();

            //Stopwatch cast = Stopwatch.StartNew();

            Buffer.BlockCopy(buffer, 0, uintData, 0, totalLength);
            ArrayPool<byte>.Shared.Return(buffer);
            //cast.Stop();

            int offset = 0;

            int totalNodeCount = (int)uintData[offset++]; // Number of nodes in the complete/global grapth
            int ownNodeCount = (int)uintData[offset++]; // Number of node current worker have

            // Allocate a array of pointers for the totalNodeCount
            // we only save a pointer to the neighbors and thus not too large but fast for lookup
            // if a worker, worskcase, try to access a node it do not have, an emppty array is returned
            var neighborSegments = new ArraySegment<uint>[totalNodeCount];

            //Stopwatch parse = Stopwatch.StartNew();
            for (int i = 0; i < ownNodeCount; i++)
            {
                uint nodeId = uintData[offset++];
                int neighborCount = (int)uintData[offset++];

                // specify where the neighbors for nodeId are located in uintData
                neighborSegments[nodeId] = new ArraySegment<uint>(uintData, offset, neighborCount);

                // skip ahead to next nodeId
                offset += neighborCount;
            }
            //parse.Stop();

            //total.Stop();

            //Console.WriteLine($"Worker total: {total.ElapsedMilliseconds} ms, " +
            //    $"Alloc: {alloc.ElapsedMilliseconds}, " +
            //    $"Cast: {cast.ElapsedMilliseconds}, " +
            //    //$"Network: {network.ElapsedMilliseconds}, " +
            //    $"Parse: {parse.ElapsedMilliseconds}");

            return (neighborSegments, totalNodeCount);
        }
    }
}
