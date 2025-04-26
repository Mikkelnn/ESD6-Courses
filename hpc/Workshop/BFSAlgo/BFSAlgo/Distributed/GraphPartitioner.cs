using System;
using System.Collections.Generic;

namespace BFSAlgo.Distributed
{
    public static class GraphPartitioner
    {
        public static List<uint>[] Partition(List<uint>[] graph, int partitions, bool evenByNode)
        {
            var partitionedGraphs = new List<uint>[partitions];

            for (int i = 0; i < partitions; i++)
                partitionedGraphs[i] = new List<uint>();

            for (uint i = 0; i < graph.Length; i++)
            {
                int partitionId = evenByNode
                    ? (int)(i % (uint)partitions)
                    : (int)((graph[i].Count * partitions) / (double)graph.Length) % partitions; // simple heuristic

                partitionedGraphs[partitionId].Add(i);
            }

            return partitionedGraphs;
        }
    }

}
