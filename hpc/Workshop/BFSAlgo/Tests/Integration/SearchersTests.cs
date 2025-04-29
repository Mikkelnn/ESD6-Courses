using BFSAlgo.Distributed;
using BFSAlgo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Integration
{
    public class SearchersTests
    {
        private List<uint>[] BuildGraph(params (uint from, uint to)[] edges)
        {
            int maxNode = edges.Length > 0 ? (int)edges.Max(e => Math.Max(e.from, e.to)) : 0;
            var graph = new List<uint>[maxNode + 1];
            for (int i = 0; i <= maxNode; i++)
                graph[i] = new List<uint>();

            foreach (var (from, to) in edges)
                graph[from].Add(to);

            return graph;
        }

        [Fact]
        public void BFS_Sequential_Should_Visit_All_Reachable_Nodes()
        {
            var graph = BuildGraph((0, 1), (0, 2), (1, 3), (2, 3));

            // Act
            var visited = Searchers.BFS_Sequential(graph, 0);

            // Assert
            for (uint i = 0; i <= 3; i++)
            {
                Assert.True(visited.Get(i), $"Node {i} should be visited");
            }
        }

        [Fact]
        public void BFS_Sequential_Should_Not_Visit_Unreachable_Nodes()
        {
            var graph = BuildGraph((0, 1), (1, 2)); // Node 3 is isolated
            ulong expectedVisitedMask = 1UL | (1UL << 1) | (1UL << 2);

            // Act
            var visited = Searchers.BFS_Sequential(graph, 0);

            // Assert
            var visitedData = visited.AsReadOnlyMemory.ToArray();
            Assert.Equal(expectedVisitedMask, BitConverter.ToUInt64(visitedData));
        }

        [Fact]
        public void BFS_Sequential_Should_Throw_On_EmptyGraph()
        {
            var graph = new List<uint>[0];

            Assert.Throws<IndexOutOfRangeException>(() => Searchers.BFS_Sequential(graph, 0));
        }


        [Fact]
        public void BFS_Parallel_Should_Visit_All_Reachable_Nodes()
        {
            var graph = BuildGraph((0, 1), (1, 2), (2, 3));

            // Act
            var visited = Searchers.BFS_Parallel(graph, 0, maxThreads: 4);

            // Assert
            for (uint i = 0; i <= 3; i++)
            {
                Assert.True(visited.Get(i), $"Node {i} should be visited");
            }
        }

        [Fact]
        public void BFS_Parallel_Should_Not_Visit_Unreachable_Nodes()
        {
            var graph = BuildGraph((0, 1), (2, 3)); // Disconnected parts

            // Act
            var visited = Searchers.BFS_Parallel(graph, 0, maxThreads: 2);

            // Assert
            Assert.False(visited.Get(2), "Node 2 should not be visited");
            Assert.False(visited.Get(3), "Node 3 should not be visited");
        }


        [Fact]
        public void BFS_Distributed_Should_Visit_All_Reachable_Nodes()
        {
            var graph = BuildGraph((0, 1), (1, 2), (2, 3));
            int millisecondsTimeout = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;

            // Act
            var visited = Searchers.BFS_Distributed(graph, 0, numWorkers: 2, millisecondsTimeout);

            // Assert
            for (uint i = 0; i <= 3; i++)
            {
                Assert.True(visited.Get(i), $"Node {i} should be visited");
            }
        }

        [Fact]
        public void BFS_Distributed_Should_Handle_SingleNodeGraph()
        {
            var graph = new List<uint>[1] { new List<uint>() };
            int millisecondsTimeout = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;

            // Act
            var visited = Searchers.BFS_Distributed(graph, 0, numWorkers: 1, millisecondsTimeout);

            // Assert
            Assert.True(visited.Get(0), "Node 0 should be visited");
        }
    }
}
