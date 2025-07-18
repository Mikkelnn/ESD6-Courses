﻿using System;
using System.Collections.Generic;

namespace BFSAlgo.Distributed
{
    public static class GraphPartitioner
    {
        // Roundrobin partition
        public static List<uint>[] Partition(List<uint>[] graph, int partitions)
        {
            if (partitions <= 0)
                throw new ArgumentOutOfRangeException(nameof(partitions), "Partition count must be greater than zero.");

            // Initialize partition map
            var partitionedGraphs = new List<uint>[partitions];
            for (int i = 0; i < partitions; i++)
                partitionedGraphs[i] = new List<uint>();

            for (uint i = 0; i < graph.Length; i++)
            {
                int partitionId = (int)(i % (uint)partitions);
                partitionedGraphs[partitionId].Add(i);
            }

            return partitionedGraphs;
        }
    }
}
