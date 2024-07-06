namespace Converter.Lemur.Graphs
{
    public class Graph
    {
        public Dictionary<Node, List<Node>> adjacencyList;

        public Graph()
        {
            adjacencyList = new Dictionary<Node, List<Node>>();
        }

        public override string ToString()
        {
            // Return the number of nodes and edges in the graph
            return $"Graph with {adjacencyList.Count} nodes and {adjacencyList.Values.Sum(x => x.Count)} edges and a population of {Population()}" +
            "\n Nodes: " + string.Join(", ", adjacencyList.Keys.Select(x => x.Name));
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


        public static int DetermineNumberOfPartitions(Graph graph)
        {
            return DetermineNumberOfPartitions(graph.Population(), graph.adjacencyList.Count);
        }

        //Todo: Move this to the converter
        public static int DetermineNumberOfPartitions(int totalPopulation, int totalNodes)
        {
            // This is based on guestimate observations form CK3
            // Sparsley populated areas often have fewer baronies per county than densely populated areas

            const int HIGH_POPULATION_THRESHOLD = 13000; //TODO: Make into a setting in the converter
            const int LOW_POPULATION_THRESHOLD = 2000;
            const int MIN_PARTITIONS = 2;
            const int MAX_PARTITIONS = 5;

            if (totalNodes < MIN_PARTITIONS * 2)
            {
                return 1; // Not enough nodes for even the minimum partition size.
            }

            if (totalPopulation > HIGH_POPULATION_THRESHOLD)
            {
                return MIN_PARTITIONS;
            }
            else if (totalPopulation < LOW_POPULATION_THRESHOLD)
            {
                return MAX_PARTITIONS;
            }
            else
            {
                // Scale linearly between MIN_PARTITIONS and MAX_PARTITIONS
                //first we subtract the low threshold from the total population
                double population = totalPopulation - LOW_POPULATION_THRESHOLD;
                //then work out the range of the population
                double range = HIGH_POPULATION_THRESHOLD - LOW_POPULATION_THRESHOLD;
                //then we work out the ratio of the population to the range
                double ratio = population / range;
                // now we must work out the number of partitions
                double numberOFPartitions = (MAX_PARTITIONS - MIN_PARTITIONS) * ratio;
                //finally we add the minimum number of partitions to the ratio
                numberOFPartitions += MIN_PARTITIONS;

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
    }
}