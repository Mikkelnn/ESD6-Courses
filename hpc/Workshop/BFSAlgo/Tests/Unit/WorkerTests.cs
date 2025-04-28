using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Mocks;

namespace Tests.Unit
{
    public class WorkerTests
    {
        [Fact]
        public async Task Worker_Start_WithMockStream_ProcessesFrontierCorrectly()
        {
            // Arrange
            var assignedNodes = new List<uint> { 0, 1 };
            var fullGraph = new List<uint>[]
            {
                [1, 2],
                [0],
                [0]
            };

            var mockStream = new NetworkStreamMock();

            // Simulate receiving partial graph - this is a quick fix...
            await NetworkHelper.SendGraphPartitionAsync(mockStream, assignedNodes, fullGraph);
            mockStream.AddDataToRead(mockStream.GetWrittenData());
            mockStream.AddDataToRead(mockStream.GetWrittenData());

            // Simulate receiving frontier [0]
            mockStream.AddDataToRead(BitConverter.GetBytes(sizeof(uint))); // Length of 1 uint
            mockStream.AddDataToRead(BitConverter.GetBytes(0U));            // Frontier: [0]

            // Simulate receiving visited bitmap (all unvisited)
            //var bitmap = new Bitmap(fullGraph.Length);
            //var bitmapBytes = bitmap.AsReadOnlyMemory.ToArray();
            //mockStream.AddDataToRead(BitConverter.GetBytes(bitmapBytes.Length));
            //mockStream.AddDataToRead(bitmapBytes);

            // Simulate termination (null frontier)
            mockStream.AddDataToRead(BitConverter.GetBytes(-1)); // -1 signals termination

            var worker = new Worker(mockStream);

            // Act
            await worker.Start();

            // Assert
            // Expect that node 1 and 2 are sent as new frontier (because 0 had neighbors 1 and 2)
            var firstSendLengthBytes = mockStream.GetWrittenData();
            int firstSendLength = BitConverter.ToInt32(firstSendLengthBytes);
            Assert.Equal(8, firstSendLength); // 2 * sizeof(uint)

            var frontierBytes = mockStream.GetWrittenData();
            var frontier = new List<uint>
            {
                BitConverter.ToUInt32(frontierBytes, 0),
                BitConverter.ToUInt32(frontierBytes, 4)
            };

            Assert.Contains(1U, frontier);
            Assert.Contains(2U, frontier);

            Assert.True(mockStream.WriteQueueLength == 0, "Worker wrote more than expected!");
            Assert.True(mockStream.ReadQueueLength == 0, "Worker did not read all data!");
        }
    }
}
