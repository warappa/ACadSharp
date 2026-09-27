using ACadSharp;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace ACadSharp.Viewer;

/// <summary>
/// Debug mode: for each dynamic block (optionally filtered by property name),
/// prints the ancestor subgraph's nodes (index / type / depth) and edges
/// (from → to, feedback/lookup/value, wire, label).
/// </summary>
static class EdgeInspector
{
    public static int Run(string filePath, string? filter)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"file not found: {filePath}");
            return 1;
        }

        CadDocument doc;
        try
        {
            doc = CadFileService.LoadFile(filePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"load failed: {ex.Message}");
            return 1;
        }

        int shown = 0;
        foreach (BlockRecord block in doc.BlockRecords)
        {
            EvaluationGraph? graph = block.EvaluationGraph;
            BlockModel? model = BlockModel.Create(block);
            if (graph is null || model is null)
            {
                continue;
            }

            foreach (PropertyItem property in model.Properties)
            {
                if (filter is not null && !property.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GraphModel.Result sub = GraphModel.BuildAncestors(graph, property.NodeIndex);
                shown++;
                Console.WriteLine();
                Console.WriteLine($"=== {block.Name} / {property.Name} ({sub.Nodes.Count} nodes, {sub.Edges.Count} edges, maxDepth={sub.MaxDepth}) ===");

                Dictionary<int, string> label = new();
                foreach (GraphNodeInfo node in sub.Nodes)
                {
                    label[node.Index] = $"{node.Expression.GetType().Name}#{node.Index}";
                }

                foreach (GraphNodeInfo node in sub.Nodes)
                {
                    Console.WriteLine($"  node {node.Index,2} (d{node.Depth}): {node.Expression.GetType().Name}");
                }
                Console.WriteLine("  edges:");
                foreach (GraphEdgeInfo edge in sub.Edges)
                {
                    string kind = edge.IsFeedback ? "FEEDBACK" : (edge.IsDashed ? "lookup  " : "value   ");
                    string from = label.GetValueOrDefault(edge.FromIndex, $"?{edge.FromIndex}");
                    string to = label.GetValueOrDefault(edge.ToIndex, $"?{edge.ToIndex}");
                    Console.WriteLine($"  {kind}  {from,-30} -> {to,-30} wire {edge.WireIndex}/{edge.WireCount}  \"{edge.Label}\"");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Edge result: {shown} property subgraph(s) shown");
        return 0;
    }
}
