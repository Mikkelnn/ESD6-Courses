using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Mocks
{
    public class NetworkStreamMock : INetworkStream
    {
        private readonly Queue<byte[]> _writeQueue = new Queue<byte[]>();
        private readonly Queue<byte[]> _readQueue = new Queue<byte[]>();

        public int WriteQueueLength => _writeQueue.Count;
        public int ReadQueueLength => _readQueue.Count;

        public void AddDataToRead(byte[] data) => _readQueue.Enqueue(data);

        public byte[] GetWrittenData() => _writeQueue.Dequeue();

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            var data = new byte[count];
            Array.Copy(buffer, offset, data, 0, count);
            _writeQueue.Enqueue(data);
            await Task.CompletedTask;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _writeQueue.Enqueue(buffer.ToArray());
            return default;
        }

        public ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_readQueue.Count == 0) return default;

            var data = _readQueue.Dequeue();
            data.CopyTo(buffer);

            return default;
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public void Close()
        {
            
        }
    }
}
