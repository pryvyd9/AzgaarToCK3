using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Converter;
using Converter.Lemur.Graphs;


public class GraphTest
{
    public static void Run()
    {

        Converter.Lemur.Helper.PrintSectionHeader("Uruk Graph Test");

        Graph urukGraph = UrukGraph();
        DetailedPrint(urukGraph);

        //Section header for this part of the code
        Converter.Lemur.Helper.PrintSectionHeader("Biggest, then lonliest, then smallest node");
        Graph.PartitionGraph(urukGraph);


        // Section: Bostoten Graph Test
        Converter.Lemur.Helper.PrintSectionHeader("Bostoten Graph Test");

        Graph bostotenGraph = BostotenGraph();
        DetailedPrint(bostotenGraph);

        //Section header for this part of the code
        Converter.Lemur.Helper.PrintSectionHeader("Biggest, then lonliest, then smallest node");
        Graph.PartitionGraph(bostotenGraph);



    }

    private static void DetailedPrint(Graph graph)
    {
        //for each node in the graph, with its name and population and the names of its neighbors
        foreach (var node in graph.adjacencyList)
        {
            Console.WriteLine($"Node: {node.Key.Name}, Population: {node.Key.Population}");
            Console.WriteLine("Neighbors:");
            foreach (var neighbor in node.Value)
            {
                Console.WriteLine($"- {neighbor.Name}");
            }
        }
    }

    private static Graph UrukGraph()
    {
        Graph graph = new(null!);
        // Create figma example graph
        Node Q = new() { Name = "Q", Population = 1014 };
        Node DR = new() { Name = "DR", Population = 2383 };
        Node G = new() { Name = "G", Population = 392 };
        Node C = new() { Name = "C", Population = 2443 };
        Node M = new() { Name = "M", Population = 5470 };
        Node Du = new() { Name = "Du", Population = 911 };

        // Add nodes to the graph
        graph.AddNode(Q);
        graph.AddNode(DR);
        graph.AddNode(G);
        graph.AddNode(C);
        graph.AddNode(M);
        graph.AddNode(Du);

        // Add edges to the graph
        graph.AddEdge(M, [Q, DR, G, C, Du]);
        graph.AddEdge(Du, Q);
        graph.AddEdge(Q, DR);
        graph.AddEdge(DR, G);
        graph.AddEdge(G, C);

        Console.WriteLine("Uruk graph created successfully!");

        // Print the graph
        Console.WriteLine(graph);

        return graph;
    }

    private static Graph BostotenGraph()
    {
        Graph graph = new(null!);

        // Create nodes for Bostoten graph
        Node He = new() { Name = "He", Population = 1116 };
        Node Hv = new() { Name = "Hv", Population = 10150 };
        Node Br = new() { Name = "Br", Population = 18661 };
        Node Dj = new() { Name = "Dj", Population = 5853 };
        Node Le = new() { Name = "Le", Population = 1585 };
        Node Re = new() { Name = "Re", Population = 1625 };

        // Add nodes to the graph
        graph.AddNode(He);
        graph.AddNode(Hv);
        graph.AddNode(Br);
        graph.AddNode(Dj);
        graph.AddNode(Le);
        graph.AddNode(Re);

        // Add edges to the graph
        graph.AddEdge(He, [Hv, Re]);
        graph.AddEdge(Hv, [Re,Br]);
        graph.AddEdge(Br, Dj);
        graph.AddEdge(Dj, Le);
        graph.AddEdge(Le, Re);

        Console.WriteLine("Bostoten graph created successfully!");

        // Print the graph
        Console.WriteLine(graph);

        return graph;
    }
}