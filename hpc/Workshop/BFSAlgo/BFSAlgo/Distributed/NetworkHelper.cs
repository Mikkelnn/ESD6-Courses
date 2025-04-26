using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo.Distributed
{
    public static class NetworkHelper
    {
        public static void SendUintArray(NetworkStream stream, List<uint> data)
        {
            if (data == null)
            {
                var lenBytes = BitConverter.GetBytes(-1);
                stream.Write(lenBytes, 0, 4);
                return;
            }

            var uintArray = data.ToArray();
            var length = uintArray.Length;

            // send length of data first so we know the amount we will recieve at the other end
            var lengthBytes = BitConverter.GetBytes(length);
            stream.Write(lengthBytes, 0, 4);

            // Read the array as a byte array efficently (almost like using a direct byte* to the uint array)
            var span = MemoryMarshal.AsBytes<uint>(uintArray.AsSpan());
            stream.Write(span);
        }

        public static List<uint> ReceiveUintArray(NetworkStream stream)
        {
            // recieve the length of the data
            var lengthBytes = new byte[4];
            if (stream.Read(lengthBytes, 0, 4) != 4) return null;

            int length = BitConverter.ToInt32(lengthBytes, 0);
            if (length == -1) return null; // Termination signal


            // allocate buffer and write to it like a byte array
            var uintArray = new uint[length];
            var byteSpan = MemoryMarshal.AsBytes<uint>(uintArray.AsSpan());

            int bytesRead = 0;
            while (bytesRead < byteSpan.Length)
            {
                int read = stream.Read(byteSpan.Slice(bytesRead));
                if (read == 0) throw new EndOfStreamException();
                bytesRead += read;
            }

            return new List<uint>(uintArray);
        }

        public static async Task SendUintArrayAsync(NetworkStream stream, List<uint> data)
        {
            if (data == null)
            {
                var lenBytes = BitConverter.GetBytes(-1);
                await stream.WriteAsync(lenBytes, 0, 4);  // Send termination signal
                return;
            }

            var uintArray = data.ToArray();                      // allocate once
            var byteSpan = MemoryMarshal.AsBytes(uintArray.AsSpan()); // span view over uint array
            var byteMemory = new ReadOnlyMemory<byte>(byteSpan.ToArray()); // Make ReadOnlyMemory<byte> from byteSpan

            // Send length of data first so we know the amount to receive at the other end
            var lengthBytes = BitConverter.GetBytes(uintArray.Length);
            await stream.WriteAsync(lengthBytes, 0, 4);

            await stream.WriteAsync(byteMemory);
        }

        public static async Task<List<uint>> ReceiveUintArrayAsync(NetworkStream stream, Stopwatch sw = null)
        {
            // Read 4 bytes for the length first
            var lengthBytes = new byte[4];
            int bytesRead = 0;
            while (bytesRead < 4)
            {
                int read = await stream.ReadAsync(lengthBytes.AsMemory(bytesRead, 4 - bytesRead));
                if (read == 0) throw new EndOfStreamException();
                bytesRead += read;
            }

            int length = BitConverter.ToInt32(lengthBytes, 0);
            if (length == -1) return null; // Termination signal

            var byteLength = length * sizeof(uint);

            sw?.Start(); // start if not null

            // Allocate space for the uint array
            var byteMemory = new byte[byteLength].AsMemory();

            // Create a span view of the uint array as bytes

            // Now receive exactly the correct number of bytes
            bytesRead = 0;
            while (bytesRead < byteLength)
            {
                int read = await stream.ReadAsync(byteMemory.Slice(bytesRead));
                if (read == 0) throw new EndOfStreamException();
                bytesRead += read;
            }

            var uintSpan = MemoryMarshal.Cast<byte, uint>(byteMemory.Span);

            // Done, convert to List<uint> and return
            return uintSpan.ToArray().ToList();
        }
    }
}
