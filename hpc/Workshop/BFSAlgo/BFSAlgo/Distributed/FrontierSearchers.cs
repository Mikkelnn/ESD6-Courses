using System.Collections.Concurrent;

namespace BFSAlgo.Distributed
{
    /// <summary>
    /// Searchers for a single frontier, used by workers
    /// </summary>
    public class FrontierSearchers
    {
        /// <summary>
        /// Seach a single frontier and return the next
        /// </summary>
        /// <param name="frontier">Set of nodes to search, each value should correspond to an index in <param name="partialGraph"/></param>
        /// <param name="partialGraph">A partial graph to serch, the length shuld equal the global graph node count</param>
        /// <param name="visited">The currently visited nodes, this will be updated as <paramref name="frontier"/> is searched</param>
        /// <returns>A list of nodes for the next frontier</returns>
        public static List<uint> SearchFrontier(Span<uint> frontier, ArraySegment<uint>[] partialGraph, Bitmap visited)
        {
            List<uint> nextFrontier = new();
            if (frontier.IsEmpty) return nextFrontier;

            for (int i = 0, fl = frontier.Length; i < fl; i++)
            {
                var neighbors = partialGraph[frontier[i]];

                for (int j = 0, length = neighbors.Count; j < length; j++)
                {
                    uint neighbor = neighbors[j];
                    if (visited.SetIfNot(neighbor))
                        nextFrontier.Add(neighbor);
                }
            }

            return nextFrontier;
        }

        /// <summary>
        /// Seach a single frontier and return the next
        /// </summary>
        /// <param name="frontier">Set of nodes to search, each value should correspond to an index in <param name="partialGraph"/></param>
        /// <param name="partialGraph">A partial graph to serch, the length shuld equal the global graph node count</param>
        /// <param name="visited">The currently visited nodes, this will be updated as <paramref name="frontier"/> is searched</param>
        /// <param name="maxThreads">The maximum number of parallell threads at any given time, excluding current</param>
        /// <returns>A list of nodes for the next frontier</returns>
        public static List<uint> SearchFrontierParallel(uint[] frontier, ArraySegment<uint>[] partialGraph, Bitmap visited, int maxThreads)
        {
            ConcurrentBag<uint> nextFrontier = new();
            if (frontier.Length == 0) return nextFrontier.ToList();


            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads }; // e.g., 4

            Parallel.For(0, frontier.Length, options, searchNode);
            void searchNode(int frontierIdx)
            {
                var neighbors = partialGraph[frontier[frontierIdx]];
                for (int i = 0, length = neighbors.Count; i < length; i++)
                {
                    // Atomically mark visited
                    uint neighbor = neighbors[i];
                    if (visited.SetIfNot(neighbor))
                        nextFrontier.Add(neighbor);
                }
            }

            return nextFrontier.ToList();

        }
    }
}
