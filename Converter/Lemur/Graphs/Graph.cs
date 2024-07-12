using System.Text;

namespace Converter.Lemur.Graphs
{
    public class Graph(Graph parentGraph)
    {
        public Dictionary<Node, List<Node>> adjacencyList = new Dictionary<Node, List<Node>>();
        public Graph? parentGraph = parentGraph;

        public override string ToString()
        {
            // Return the number of nodes and edges in the graph
            return $"Graph with {adjacencyList.Count} nodes, {adjacencyList.Values.Sum(x => x.Count)} edges and a population of {Population()}" +
            "\n Nodes: " + string.Join(", ", adjacencyList.Keys.Select(x => x.Name));
        }

        public string ToDetailedString()
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine($"============ Detailed Graph ============");
            stringBuilder.AppendLine($"Graph with {adjacencyList.Count} nodes\n");


            
            // Then for each node add teh node and its adjacent nodes
            foreach (var node in adjacencyList)
            {
                stringBuilder.AppendLine($"Node: {node.Key.Name}, Population: {node.Key.Population}");
                stringBuilder.AppendLine("Neighbors:");
                foreach (var neighbor in node.Value)
                {
                    stringBuilder.AppendLine($"- {neighbor.Name}. Isolated: {(neighbor.Isolated ? "[x]" : "[ ]")}. InSubGraph: {(neighbor.InSubGraph ? "[x]" : "[ ]")}");
                }
            }

            return stringBuilder.ToString();

        }

        public void AddNode(Node node)
        {
            // Ensure the node is added to the adjacency list with an empty list of connected nodes
            if (!adjacencyList.ContainsKey(node))
            {
                adjacencyList[node] = new List<Node>();
            }
            else
            {
                throw new ArgumentException("Node already exists in the graph");
            }
        }

        public void AddEdge(Node source, List<Node> destinations, bool directed = false)
        {
            foreach (var destination in destinations)
            {
                AddEdge(source, destination, directed);
            }
        }

        public void AddNodeWithEdges(Node node, List<Node> destinations, bool directed = false)
        {
            AddNode(node);
            AddEdge(node, destinations, directed);
        }
        public void AddEdge(Node source, Node destination, bool directed = false)
        {
            //if the destination is not in the graph, ignore it
            if (!adjacencyList.ContainsKey(destination))
            {
                return;
            }

            // Add the destination node to the source's list of connected nodes
            if (!adjacencyList.TryGetValue(source, out List<Node>? adjacensies))
            {
                adjacensies = new List<Node>();
                adjacencyList[source] = adjacensies;
            }

            adjacensies.Add(destination);

            if (directed)
            {
                return;
            }

            // Add the source node to the destination's list of connected nodes
            if (!adjacencyList.TryGetValue(destination, out List<Node>? adjacensiesReverse))
            {
                adjacensiesReverse = new List<Node>();
                adjacencyList[destination] = adjacensiesReverse;
            }

            adjacensiesReverse.Add(source);
        }

        //population of the graph
        public int Population()
        {
            return adjacencyList.Keys.Sum(x => x.Population);
        }

        internal List<Node> GetNodes()
        {
            return adjacencyList.Keys.ToList();
        }


        //Todo: Move this to the converter
        public static int DetermineNumberOfPartitions(Graph graph)
        {

            int highPopulationThreshold = Settings.Instance.HighPopulationThreshold;
            int lowPopulationThreshold = Settings.Instance.LowPopulationThreshold;
            int minCounties = Settings.Instance.MinCounties;
            int maxCounties = Settings.Instance.MaxCounties;

            int totalPopulation = graph.Population();
            int totalNodes = graph.adjacencyList.Count;

            if (totalNodes < minCounties * 2)
            {
                return 1; // Not enough nodes for even the minimum partition size.
            }

            if (totalPopulation > highPopulationThreshold)
            {
                return minCounties;
            }
            else if (totalPopulation < lowPopulationThreshold)
            {
                return maxCounties;
            }
            else
            {
                // Scale linearly between MIN_PARTITIONS and MAX_PARTITIONS
                //first we subtract the low threshold from the total population
                double population = totalPopulation - lowPopulationThreshold;
                //then work out the range of the population
                double range = highPopulationThreshold - lowPopulationThreshold;
                //then we work out the ratio of the population to the range
                double ratio = population / range;
                // now we must work out the number of partitions
                double numberOFPartitions = (maxCounties - minCounties) * ratio;
                //finally we add the minimum number of partitions to the ratio
                numberOFPartitions += minCounties;

                //then we round the number of partitions to the nearest whole number
                double partitionsBasedOnPop = (int)Math.Floor(numberOFPartitions);

                //however, since no partition can be of node size 1, 
                // we must check if each partition could have at least 2 nodes
                if (totalNodes / partitionsBasedOnPop < 2)
                {
                    //if not, we must reduce the number of partitions so that it is
                    // easiest way to do this is to divide the total number of nodes by 2
                    partitionsBasedOnPop = totalNodes / 2;
                }
                return (int)partitionsBasedOnPop;

            }
        }

        internal Node GetMostPopulousNode()
        {
            //Get the node with the highest population
            return adjacencyList.Keys.OrderByDescending(x => x.Population).First();
        }

        public List<Node> GetAdjacentNodesInParentGraph()
        {
            //First, sanity check that this graph is a partition of the parent graph
            if (adjacencyList.Keys.Any(x => !parentGraph.adjacencyList.ContainsKey(x)))
            {
                throw new ArgumentException("This graph is not a partition of the parent graph");
            }
            //Given a parent graph, return all the nodes that are adjacent to the current graph
            //every neighbour of every node in the graph:
            List<Node> adjacentNodes = new List<Node>();

            foreach (var node in GetNodes())
            {
                //look up the node in the parent graph
                if (parentGraph.adjacencyList.TryGetValue(node, out List<Node>? neighbours))
                {
                    //add all the neighbours to the list of adjacent nodes
                    adjacentNodes.AddRange(neighbours);
                }
            }
            //Remove any nodes that are in the partition so as only to return nodes that are adjacent to the partition
            return adjacentNodes.Where(x => !adjacencyList.ContainsKey(x)).Distinct().ToList();
        }
        public static List<Graph> PartitionGraph(Graph graph)
        {


            // Determine the number of partitions for this graph
            int numberOfPartitions = DetermineNumberOfPartitions(graph);
            if (Settings.Instance.Debug)
            {
                Console.WriteLine($"Partitioning graph {graph}. Ideal # partitions: {numberOfPartitions}");
            }

            if (numberOfPartitions == 1)
            {
                Console.WriteLine("Graph is already a single partition");
                return [graph];
            }

            // Create a list of partitions
            List<Graph> partitions = new(numberOfPartitions);

            //while there are nodes not assigned a partition
            while (partitions.Count < numberOfPartitions)
            {
                // Check if there are any nodes left to distribute. Must be at least 2 nodes left to form a new partition
                var undistributedNodesCount = graph.GetNodes().Count(x => !x.InSubGraph && !x.Isolated);
                if (undistributedNodesCount <= 1)
                {
                    Console.WriteLine(undistributedNodesCount == 0 ? "All nodes have been distributed" : "Only one node left, too few to form a new partition");
                    break;
                }

                // Create a new partition
                Graph partition = new(graph);

                // Start with the most populous node that is not yet assigned to a partition
                Node mostPopulousNode = graph.GetNodes().OrderByDescending(x => x.Population).Where(x => x.InSubGraph == false).Where(x => x.Isolated == false).First();
                partition.AddNodeWithEdges(mostPopulousNode, graph.adjacencyList[mostPopulousNode]);

                //Get all the adjacent nodes of the current partition, not in a subgraph
                List<Node> adjacentNodes = partition.GetAdjacentNodesInParentGraph().Where(x => x.InSubGraph == false).ToList();

                // If this is an isolated node, then this cannot be the basis of a partition. Mark it as isolated and continue
                if (adjacentNodes.Count == 0)
                {
                    if (Settings.Instance.Debug) Console.WriteLine($"The node {mostPopulousNode.Name} is isolated");
                    mostPopulousNode.Isolated = true;
                    continue;
                }
                mostPopulousNode.InSubGraph = true;


                //if the adjacent node list is one, just add it to the partition
                Node isolatedSmallNeighbour;
                if (adjacentNodes.Count == 1)
                {
                    isolatedSmallNeighbour = adjacentNodes.First();
                }
                else
                {
                    // Add to temporary graph to remove edges going into the existing partion
                    var tempGraph = new Graph(graph);
                    foreach (var node in adjacentNodes)
                    {
                        tempGraph.AddNodeWithEdges(node, graph.adjacencyList[node]);
                    }
                    // Order by the fewest edges and then by population and get the first node
                    isolatedSmallNeighbour = tempGraph.GetNodes().
                    OrderBy(x => graph.adjacencyList[x].Count).
                    ThenBy(x => x.Population).ToList().First();
                }

                // Add the node to the partition
                partition.AddNodeWithEdges(isolatedSmallNeighbour, graph.adjacencyList[isolatedSmallNeighbour]);
                isolatedSmallNeighbour.InSubGraph = true;
                // Add the partition to the list of partitions
                partitions.Add(partition);

            }

            // Print the initial partitions
            Console.WriteLine("Initial partitions created");

            var leftovers = graph.GetNodes().Where(x => x.InSubGraph == false).ToList();
            if (leftovers.Count != 0)
            {
                Console.WriteLine($"Handeling leftovers...({string.Join(", ", leftovers.Select(x => x.Name))})");

                //Handle Leftovers - focus on balancing the partitions
                foreach (var node in leftovers)
                {
                    //Find bordering partitions, do this by finding adajcent nodes and checking what partitions they are in
                    List<Graph> borderingPartitions = new List<Graph>();
                    foreach (var neighbour in graph.adjacencyList[node]) // For each neighbour of the node
                    {
                        foreach (var partition in partitions)
                        {
                            if (partition.adjacencyList.ContainsKey(neighbour))
                            {
                                borderingPartitions.Add(partition);
                            }
                        }
                    }

                    // Add this node to the partition where adding it would bring the partition closest to the ideal population
                    int idealPopulation = graph.Population() / numberOfPartitions;
                    Graph targetPartition = borderingPartitions.OrderBy(x => Math.Abs(x.Population() + node.Population - idealPopulation)).First();
                    targetPartition.AddNodeWithEdges(node, graph.adjacencyList[node]);
                }

            }

            // Print the final partitions
            Console.WriteLine("Final partitions created:");
            foreach (var partition in partitions)
            {
                Console.WriteLine(partition);
            }

            return partitions;
        }
    }
}