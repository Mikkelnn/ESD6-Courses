using BFSAlgo.Distributed;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Unit
{
    public class GraphPartitionerTests
    {
        [Fact]
        public void Partition_ShouldCreateCorrectNumberOfPartitions()
        {
            var graph = new List<uint>[10];
            for (int i = 0; i < graph.Length; i++)
                graph[i] = new List<uint>();

            int partitionCount = 3;
            var partitions = GraphPartitioner.Partition(graph, partitionCount);

            Assert.Equal(partitionCount, partitions.Length);
        }

        [Fact]
        public void Partition_ShouldDistributeNodesAcrossPartitions()
        {
            var graph = new List<uint>[6];
            for (int i = 0; i < graph.Length; i++)
                graph[i] = new List<uint>();

            int partitionCount = 2;
            var partitions = GraphPartitioner.Partition(graph, partitionCount);

            var allNodes = new List<uint>();
            foreach (var partition in partitions)
                allNodes.AddRange(partition);

            Assert.Equal(6, allNodes.Count);
            Assert.All(allNodes, node => Assert.InRange(node, 0u, 5u));
            Assert.Equal(6, new HashSet<uint>(allNodes).Count); // No duplicates
        }

        [Fact]
        public void Partition_ShouldAssignNodesInRoundRobin()
        {
            var graph = new List<uint>[4];
            for (int i = 0; i < graph.Length; i++)
                graph[i] = new List<uint>();

            int partitionCount = 2;
            var partitions = GraphPartitioner.Partition(graph, partitionCount);

            // Nodes 0 and 2 should go to partition 0
            Assert.Contains(0u, partitions[0]);
            Assert.Contains(2u, partitions[0]);

            // Nodes 1 and 3 should go to partition 1
            Assert.Contains(1u, partitions[1]);
            Assert.Contains(3u, partitions[1]);
        }

        [Fact]
        public void Partition_WithOnePartition_ShouldPlaceAllNodesInOnePartition()
        {
            var graph = new List<uint>[5];
            for (int i = 0; i < graph.Length; i++)
                graph[i] = new List<uint>();

            int partitionCount = 1;
            var partitions = GraphPartitioner.Partition(graph, partitionCount);

            Assert.Single(partitions);
            Assert.Equal(5, partitions[0].Count);
        }

        [Fact]
        public void Partition_WithMorePartitionsThanNodes_ShouldHandleEmptyPartitions()
        {
            var graph = new List<uint>[3];
            for (int i = 0; i < graph.Length; i++)
                graph[i] = new List<uint>();

            int partitionCount = 5;
            var partitions = GraphPartitioner.Partition(graph, partitionCount);

            Assert.Equal(partitionCount, partitions.Length);

            var allNodes = new List<uint>();
            foreach (var partition in partitions)
                allNodes.AddRange(partition);

            Assert.Equal(3, allNodes.Count);
            Assert.Contains(0u, allNodes);
            Assert.Contains(1u, allNodes);
            Assert.Contains(2u, allNodes);

            int emptyPartitions = partitions.Count(partition => partition.Count == 0);
            Assert.Equal(2, emptyPartitions); // Two partitions should be empty
        }
    }
}
