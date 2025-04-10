using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo
{
    internal class Searchers
    {
        public static void BFS_Sequential(List<int>[] graph, int startNode)
        {
            var visited = new bool[graph.Length];
            var queue = new Queue<int>();
            visited[startNode] = true;
            queue.Enqueue(startNode);

            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                foreach (var neighbor in graph[node])
                {
                    if (!visited[neighbor])
                    {
                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }
}
