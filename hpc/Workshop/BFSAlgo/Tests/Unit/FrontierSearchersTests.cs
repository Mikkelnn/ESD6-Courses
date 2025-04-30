using BFSAlgo.Distributed;

namespace Tests.Unit
{
    public class FrontierSearchersTests
    {
        [Fact]
        public void SearchFrontier_ReturnsCorrectNextFrontier()
        {
            // Arrange
            var partialGraph = new ArraySegment<uint>[5];
            partialGraph[0] = new ArraySegment<uint>(new uint[] { 1, 2 });
            partialGraph[1] = new ArraySegment<uint>(new uint[] { 3 });
            partialGraph[2] = new ArraySegment<uint>(new uint[] { 4 });
            partialGraph[3] = new ArraySegment<uint>(new uint[] { });
            partialGraph[4] = new ArraySegment<uint>(new uint[] { });

            var visited = new Bitmap(5); // or whatever capacity matches your actual Bitmap usage
            Span<uint> frontier = new uint[] { 0 };

            // Act
            var nextFrontier = FrontierSearchers.SearchFrontier(frontier, partialGraph, visited);

            // Assert
            Assert.Equal(new List<uint> { 1, 2 }, nextFrontier);
        }

        [Fact]
        public void SearchFrontierParallel_ReturnsSameAsSequential()
        {
            // Arrange
            var partialGraph = new ArraySegment<uint>[5];
            partialGraph[0] = new ArraySegment<uint>(new uint[] { 1, 2 });
            partialGraph[1] = new ArraySegment<uint>(new uint[] { 3 });
            partialGraph[2] = new ArraySegment<uint>(new uint[] { 4 });
            partialGraph[3] = new ArraySegment<uint>(new uint[] { });
            partialGraph[4] = new ArraySegment<uint>(new uint[] { });

            var visitedSequential = new Bitmap(5);
            var visitedParallel = new Bitmap(5);
            var frontier = new uint[] { 0 };

            // Act
            var expected = FrontierSearchers.SearchFrontier(frontier, partialGraph, visitedSequential);
            var actual = FrontierSearchers.SearchFrontierParallel(frontier, partialGraph, visitedParallel, maxThreads: 4);

            // Assert (unordered equivalence)
            Assert.Equal(expected.Count, actual.Count);
            Assert.All(expected, e => Assert.Contains(e, actual));
        }

        [Fact]
        public void SearchFrontier_EmptyFrontier_ReturnsEmpty()
        {
            var visited = new Bitmap(0);
            Span<uint> frontier = Array.Empty<uint>();
            var partialGraph = new ArraySegment<uint>[0];

            var result = FrontierSearchers.SearchFrontier(frontier, partialGraph, visited);

            Assert.Empty(result);
        }

        [Fact]
        public void SearchFrontierParallel_EmptyFrontier_ReturnsEmpty()
        {
            var visited = new Bitmap(0);
            var frontier = Array.Empty<uint>();
            var partialGraph = new ArraySegment<uint>[0];

            var result = FrontierSearchers.SearchFrontierParallel(frontier, partialGraph, visited, maxThreads: 2);

            Assert.Empty(result);
        }
        
        [Fact]
        public void SearchFrontier_SkipsAlreadyVisitedNodes()
        {
            // Arrange
            var partialGraph = new ArraySegment<uint>[3];
            partialGraph[0] = new ArraySegment<uint>(new uint[] { 1 });
            partialGraph[1] = new ArraySegment<uint>(new uint[] { 2 });
            partialGraph[2] = new ArraySegment<uint>(new uint[] { });

            var visited = new Bitmap(3);
            visited.SetIfNot(1); // Mark node 1 as visited already

            Span<uint> frontier = new uint[] { 0 };

            // Act
            var nextFrontier = FrontierSearchers.SearchFrontier(frontier, partialGraph, visited);

            // Assert
            Assert.Empty(nextFrontier); // 1 is already visited, so nothing new should be added
        }

        [Fact]
        public void SearchFrontier_TraversesBreadthFirst()
        {
            // Graph:
            // 0 -> 1, 2
            // 1 -> 3
            // 2 -> 4
            var partialGraph = new ArraySegment<uint>[5];
            partialGraph[0] = new ArraySegment<uint>(new uint[] { 1, 2 });
            partialGraph[1] = new ArraySegment<uint>(new uint[] { 3 });
            partialGraph[2] = new ArraySegment<uint>(new uint[] { 4 });
            partialGraph[3] = new ArraySegment<uint>(Array.Empty<uint>());
            partialGraph[4] = new ArraySegment<uint>(Array.Empty<uint>());

            var visited = new Bitmap(5);
            var level1 = FrontierSearchers.SearchFrontier(new uint[] { 0 }, partialGraph, visited);
            var level2 = FrontierSearchers.SearchFrontier(level1.ToArray(), partialGraph, visited);
            var level3 = FrontierSearchers.SearchFrontier(level2.ToArray(), partialGraph, visited);

            Assert.Equal(new List<uint> { 1, 2 }, level1);
            Assert.Equal(new List<uint> { 3, 4 }, level2);
            Assert.Empty(level3); // No more nodes to expand
        }

        [Fact]
        public void SearchFrontierParallel_TraversesBreadthFirst()
        {
            // Graph:
            // 0 -> 1, 2
            // 1 -> 3
            // 2 -> 4
            var partialGraph = new ArraySegment<uint>[5];
            partialGraph[0] = new ArraySegment<uint>(new uint[] { 1, 2 });
            partialGraph[1] = new ArraySegment<uint>(new uint[] { 3 });
            partialGraph[2] = new ArraySegment<uint>(new uint[] { 4 });
            partialGraph[3] = new ArraySegment<uint>(Array.Empty<uint>());
            partialGraph[4] = new ArraySegment<uint>(Array.Empty<uint>());

            var visited = new Bitmap(5);

            var level1 = FrontierSearchers.SearchFrontierParallel(new uint[] { 0 }, partialGraph, visited, maxThreads: 4);
            var level2 = FrontierSearchers.SearchFrontierParallel(level1.ToArray(), partialGraph, visited, maxThreads: 4);
            var level3 = FrontierSearchers.SearchFrontierParallel(level2.ToArray(), partialGraph, visited, maxThreads: 4);

            Assert.Equal(2, level1.Count);
            Assert.Contains(1u, level1);
            Assert.Contains(2u, level1);

            Assert.Equal(2, level2.Count);
            Assert.Contains(3u, level2);
            Assert.Contains(4u, level2);

            Assert.Empty(level3);
        }

    }

}
