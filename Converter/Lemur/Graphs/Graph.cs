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
            return $"Graph with {adjacencyList.Count} nodes and {adjacencyList.Values.Sum(x => x.Count)} edges and a population of {Population()}";
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
    }
}