using BFSAlgo;
using BFSAlgo.Distributed;

namespace Tests.Unit
{
    public class GraphServiceTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Partition_ShouldThrow_WhenPartitionCountIsInvalid(int invalidPartitionCount)
        {
            var graph = new List<uint>[] { [] };
            Assert.Throws<ArgumentOutOfRangeException>(() => GraphPartitioner.Partition(graph, invalidPartitionCount));
        }

        [Fact]
        public void GenerateGraph_ShouldCreateCorrectNumberOfNodes()
        {
            uint nodeCount = 10;
            uint maxEdges = 3;

            var graph = GraphService.GenerateGraph(nodeCount, maxEdges);

            Assert.NotNull(graph);
            Assert.Equal(nodeCount, (uint)graph.Length);

            for (int i = 0; i < graph.Length; i++)
            {
                Assert.All(graph[i], neighbor => Assert.InRange(neighbor, 0u, nodeCount - 1));
            }
        }

        [Fact]
        public void GenerateGraph_ShouldNotCreateSelfLoops()
        {
            uint nodeCount = 20;
            uint maxEdges = 5;

            var graph = GraphService.GenerateGraph(nodeCount, maxEdges);

            for (int i = 0; i < graph.Length; i++)
            {
                Assert.DoesNotContain((uint)i, graph[i]);
            }
        }

        [Fact]
        public void GenerateGraph_ShouldCreateSymmetricEdges()
        {
            uint nodeCount = 20;
            uint maxEdges = 5;

            var graph = GraphService.GenerateGraph(nodeCount, maxEdges);

            for (uint i = 0; i < graph.Length; i++)
            {
                foreach (var neighbor in graph[i])
                {
                    Assert.Contains(i, graph[neighbor]);
                }
            }
        }

        [Fact]
        public void SaveAndLoadGraph_ShouldPreserveGraphStructure()
        {
            var graph = new List<uint>[]
            {
            new List<uint> { 1, 2 },
            new List<uint> { 0 },
            new List<uint> { 0 }
            };

            var path = Path.Combine(Path.GetTempPath(), "graph_test.bin");

            GraphService.SaveGraph(graph, path);
            var loadedGraph = GraphService.LoadGraph(path);

            Assert.Equal(graph.Length, loadedGraph.Length);

            for (int i = 0; i < graph.Length; i++)
            {
                Assert.Equal(graph[i], loadedGraph[i]);
            }

            File.Delete(path); // Clean up
        }

        [Fact]
        public void SaveAndLoadGraphText_ShouldPreserveGraphStructure()
        {
            var graph = new List<uint>[]
            {
            new List<uint> { 1, 2 },
            new List<uint> { 0 },
            new List<uint> { 0 }
            };

            var path = Path.Combine(Path.GetTempPath(), "graph_test.txt");

            GraphService.SaveGraphText(graph, path);
            var loadedGraph = GraphService.LoadGraphTxt(path);

            Assert.Equal(graph.Length, loadedGraph.Length);

            for (int i = 0; i < graph.Length; i++)
            {
                Assert.Equal(graph[i], loadedGraph[i]);
            }

            File.Delete(path); // Clean up
        }

        [Fact]
        public void LoadGraphTxt_ShouldHandleEmptyNeighbors()
        {
            string path = Path.Combine(Path.GetTempPath(), "empty_neighbors_test.txt");
            File.WriteAllLines(path, new[]
            {
                "0:1,2",
                "1:",
                "2:0"
            });

            var graph = GraphService.LoadGraphTxt(path);

            Assert.Equal(3, graph.Length);
            Assert.Equal(new List<uint> { 1, 2 }, graph[0]);
            Assert.Empty(graph[1]);
            Assert.Equal(new List<uint> { 0 }, graph[2]);

            File.Delete(path); // Clean up
        }
    }
}
