namespace Converter.Lemur.Graphs
{
    public class GraphEvaluator
    {
        private Graph _graph;

        public GraphEvaluator(Graph graph)
        {
            _graph = graph;
        }

        public double EvaluatePartitions(List<Graph> partitions, int targetPopulation)
        {
            double populationScore = 0;
            int internalEdgesScore = 0;
            int crossPartitionPenalty = 0;
            int contiguityPenalty = 0;

            foreach (var partition in partitions)
            {
                populationScore += Math.Abs(partition.Population() - targetPopulation);
                internalEdgesScore += CountInternalEdges(partition);

                if (!IsConnectedComponent(partition))
                {
                    contiguityPenalty += 1000; // Arbitrary high penalty for disconnected partition
                }
            }

            crossPartitionPenalty = CountCrossPartitionEdges(partitions);

            double normalizedPopulationScore = populationScore / (targetPopulation * partitions.Count);
            double normalizedInternalEdgesScore = (double)internalEdgesScore / TotalEdgesInGraph();
            double normalizedCrossPartitionPenalty = (double)crossPartitionPenalty / TotalEdgesInGraph();

            double totalScore = (1 * normalizedPopulationScore) +
                                (2 * normalizedInternalEdgesScore) -
                                (3 * normalizedCrossPartitionPenalty) -
                                (4 * contiguityPenalty);

            return totalScore;
        }

        private static int CountInternalEdges(Graph partition)
        {
            int count = 0;
            foreach (var node in partition.adjacencyList.Keys)
            {
                foreach (var neighbor in partition.adjacencyList[node])
                {
                    if (partition.adjacencyList.ContainsKey(neighbor))
                    {
                        count++;
                    }
                }
            }
            return count / 2; // Since each edge is counted twice
        }

        private static int CountCrossPartitionEdges(List<Graph> partitions)
        {
            int count = 0;
            foreach (var partition in partitions)
            {
                foreach (var node in partition.adjacencyList.Keys)
                {
                    // Access the list of neighbors from the adjacency list of the graph
                    List<Node> neighbors = partition.adjacencyList[node];
                    foreach (var neighbor in neighbors)
                    {
                        // Check if the neighbor is not in the current partition's adjacency list
                        if (!partition.adjacencyList.ContainsKey(neighbor))
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private bool IsConnectedComponent(Graph partition)
        {
            // Implement a BFS or DFS to check if all nodes in the partition are connected
            if (partition.adjacencyList.Count == 0) return true;

            HashSet<Node> visited = new HashSet<Node>();
            Queue<Node> queue = new Queue<Node>();
            queue.Enqueue(partition.adjacencyList.Keys.First());

            while (queue.Count > 0)
            {
                Node current = queue.Dequeue();
                if (!visited.Contains(current))
                {
                    visited.Add(current);
                    foreach (var neighbor in partition.adjacencyList[current])
                    {
                        if (partition.adjacencyList.ContainsKey(neighbor) && !visited.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return visited.Count == partition.adjacencyList.Count;
        }

        private int TotalEdgesInGraph()
        {
            // Return the total number of edges in the graph
            return _graph.adjacencyList.Values.Sum(x => x.Count) / 2; // Since each edge is counted twice
        }
    }
}
