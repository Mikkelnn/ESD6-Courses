using BFSAlgo.Distributed;
using System.Runtime.InteropServices;
using Tests.Mocks;

namespace Tests.Unit
{
    public class NetworkHelperTests
    {
        [Fact]
        public void ToReadOnlyMemory_ConvertsListCorrectly()
        {
            // Arrange
            var list = new List<uint> { 1, 2, 3, 4 };

            // Act
            var memory = list.ToReadOnlyMemory();

            // Assert
            Assert.Equal(list.Count * sizeof(uint), memory.Length);

            var span = MemoryMarshal.Cast<byte, uint>(memory.Span);
            Assert.Equal(list, span.ToArray());
        }
        
        [Fact]
        public void ToReadOnlyMemory_EmptyList_ReturnsEmptyMemory()
        {
            // Arrange
            var list = new List<uint>();

            // Act
            var memory = list.ToReadOnlyMemory();

            // Assert
            Assert.Equal(0, memory.Length);
        }

        [Fact]
        public async Task SendDataAsync_NullData_SendsTerminationSignal()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();

            // Act
            await NetworkHelper.SendDataAsync(mockStream, null);

            // Assert: Ensure that termination signal (-1) is sent
            var written = mockStream.GetWrittenData();
            Assert.Equal(sizeof(uint), written.Length);
            Assert.Equal(-1, BitConverter.ToInt32(written));
        }

        [Fact]
        public async Task SendDataAsync_SomeData_SendsDataCorrectly()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();

            var data = new List<uint> { 1, 2, 3, 4 };

            // Act
            await NetworkHelper.SendDataAsync(mockStream, data.ToReadOnlyMemory());

            // Assert: Ensure data length and data are being written correctly
            var writtenLength = mockStream.GetWrittenData();
            Assert.Equal(sizeof(int), writtenLength.Length);
            Assert.Equal(data.Count * sizeof(uint), BitConverter.ToInt32(writtenLength, 0));

            var writtenData = mockStream.GetWrittenData();
            Assert.Equal(data.Count * sizeof(uint), writtenData.Length);
            
            for (int i = 0; i < data.Count; i++)
                Assert.Equal(data[i], BitConverter.ToUInt32(writtenData, i * sizeof(uint)));
        }
       
        [Fact]
        public async Task ReceiveByteArrayAsync_SuccessfullyReceivesData()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();
            var expectedData = new byte[] { 10, 20, 30, 40 };

            mockStream.AddDataToRead(BitConverter.GetBytes(expectedData.Length));
            mockStream.AddDataToRead(expectedData);

            // Act
            var result = await NetworkHelper.ReceiveByteArrayAsync(mockStream);

            // Assert
            Assert.Equal(expectedData, result);
        }

        [Fact]
        public async Task ReceiveByteArrayAsync_ReceivesTerminationSignal_ReturnsNull()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();
            mockStream.AddDataToRead(BitConverter.GetBytes(-1)); // Simulate termination signal

            // Act
            var result = await NetworkHelper.ReceiveByteArrayAsync(mockStream);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ReceiveUintArrayAsync_SuccessfullyReceivesData()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();
            var data = new List<uint> { 1, 2, 3, 4 };
            var byteData = data.ToReadOnlyMemory().ToArray();

            mockStream.AddDataToRead(BitConverter.GetBytes(byteData.Length));
            mockStream.AddDataToRead(byteData);

            // Act
            var result = await NetworkHelper.ReceiveUintArrayAsync(mockStream);

            // Assert: Verify that the data was read correctly
            Assert.Equal(data, result);
        }

        [Fact]
        public async Task ReceiveUintArrayAsync_ReceivesTerminationSignal_ReturnsNull()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();
            mockStream.AddDataToRead(BitConverter.GetBytes(-1)); // Simulate termination signal

            // Act
            var result = await NetworkHelper.ReceiveUintArrayAsync(mockStream);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SendGraphPartitionAsync_SendsGraphPartitionCorrectly()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();
            var ownedNodes = new List<uint> { 0, 1, 2 };
            var fullGraph = new List<uint>[]
            {
                [1, 2],
                [0, 2],
                [0, 1]
            };

            var expectedData = new byte[]
            {
                // Data of the graph partition
                3, 0, 0, 0,           // Number of nodes (3)

                0, 0, 0, 0,           // Node 0
                2, 0, 0, 0,           // 2 neighbors for node 0
                1, 0, 0, 0,           // Neighbor 0
                2, 0, 0, 0,           // Neighbor 1
                
                1, 0, 0, 0,           // Node 1
                2, 0, 0, 0,           // 2 neighbors for node 0
                0, 0, 0, 0,           // Neighbor 0
                2, 0, 0, 0,           // Neighbor 1

                2, 0, 0, 0,           // Node 2
                2, 0, 0, 0,           // 2 neighbors for node 0
                0, 0, 0, 0,           // Neighbor 0
                1, 0, 0, 0,           // Neighbor 1
            };

            // Act
            await NetworkHelper.SendGraphPartitionAsync(mockStream, ownedNodes, fullGraph);

            // Assert: Ensure that the data is being written to the stream
            var writtenLength = mockStream.GetWrittenData();
            Assert.Equal(sizeof(int), writtenLength.Length);
            Assert.Equal(expectedData.Length, BitConverter.ToInt32(writtenLength, 0));

            var writtenData = mockStream.GetWrittenData();
            Assert.Equal(expectedData, writtenData);
        }

        [Fact]
        public async Task ReceiveGraphPartitionAsync_SuccessfullyReceivesGraphPartition()
        {
            // Arrange
            var mockStream = new NetworkStreamMock();
            var data = new byte[]
            {
                // Data of the graph partition
                2, 0, 0, 0,           // Number of nodes (2)
                0, 0, 0, 0,           // Node 0
                2, 0, 0, 0,           // 2 neighbors for node 0
                1, 0, 0, 0,           // Neighbor 0
                2, 0, 0, 0,           // Neighbor 1
                1, 0, 0, 0,           // Node 1
                1, 0, 0, 0,           // 1 neighbor for node 1
                0, 0, 0, 0            // Neighbor 0
            };

            mockStream.AddDataToRead(BitConverter.GetBytes(data.Length));
            mockStream.AddDataToRead(data);

            // Act
            var result = await NetworkHelper.ReceiveGraphPartitionAsync(mockStream);

            // Assert: Verify the graph partition is parsed correctly
            Assert.Equal(2, result.Count);
            Assert.Contains(0U, result.Keys);
            Assert.Contains(1U, result.Keys);
            Assert.Equal(new List<uint> { 1, 2 }, result[0]);
            Assert.Equal(new List<uint> { 0 }, result[1]);
        }
    }
}
