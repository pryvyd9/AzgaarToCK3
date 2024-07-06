using System;
using System.Collections.Generic;
using Converter;
using Converter.Lemur.Graphs;


public class GraphTest
{
    public static void Run()
    {

        Converter.Lemur.Helper.PrintSectionHeader("Graph Test");

        Graph graph = new();

        // Create dummy nodes
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

        Console.WriteLine("Graph created successfully!");

        // Print the graph
        Console.WriteLine(graph);
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


        //now lest try and balance the graph



    }
}