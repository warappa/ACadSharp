using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using System;
using System.Collections.Generic;
using System.Linq;

if (args.Length < 1)
{
    Console.WriteLine("usage: graph-dump <file.dwg|file.dxf> [name-substring]");
    Console.WriteLine("  no substring: dump the display model of every parameter of every dynamic block");
    Console.WriteLine("  with one:     only parameters whose ElementName contains the substring");
    return;
}

CadDocument doc;
using (var reader = new DwgReader(args[0]))
{
    doc = reader.Read();
}

string? filter = args.Length > 1 ? args[1] : null;
Console.WriteLine($"=== {System.IO.Path.GetFileName(args[0])} ===");

// A block matches when its name contains the filter, or one of its
// parameter ElementNames does.
bool Matches(BlockRecord b) =>
    b.Name.Contains(filter!, StringComparison.OrdinalIgnoreCase)
    || b.EvaluationGraph!.Nodes.Any(n => n.Expression is BlockElement be
        && !string.IsNullOrEmpty(be.ElementName)
        && be.ElementName.Contains(filter!, StringComparison.OrdinalIgnoreCase));

List<(string Name, EvaluationGraph Graph)> graphs = doc.BlockRecords
    .Where(b => b.EvaluationGraph != null)
    .Where(b => filter is null || Matches(b))
    .Select(b => (b.Name, b.EvaluationGraph))
    .ToList();

Console.WriteLine(filter is null
    ? $"scanning {graphs.Count} dynamic block(s)"
    : $"matched {graphs.Count} block(s) for '{filter}'");

foreach ((string name, EvaluationGraph graph) in graphs)
{
    // Targets: the parameter nodes (a BlockElement with a non-empty ElementName)
    // matching the filter — or all of them when no filter is given.
    List<int> targets = graph.Nodes
        .Where(n => n.Expression is BlockElement be && !string.IsNullOrEmpty(be.ElementName)
            && (filter is null || be.ElementName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        .Select(n => n.Index)
        .OrderBy(i => i)
        .ToList();

    if (targets.Count == 0)
    {
        if (filter is not null)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Block '{name}'  (nodes={graph.Nodes.Count()} edges={graph.Edges.Count}) ===");
            Console.WriteLine("  (no matching parameter found; skipping)");
        }

        continue;
    }

    Console.WriteLine();
    Console.WriteLine($"=== Block '{name}'  (nodes={graph.Nodes.Count()} edges={graph.Edges.Count}) ===");

    foreach (int target in targets)
    {
        EvaluationExpression targetExpr = graph.Nodes.First(n => n.Index == target).Expression;
        string en = (targetExpr as BlockElement)?.ElementName ?? "";

        var model = GraphModel.BuildAncestors(graph, target);

        Console.WriteLine($"  --- '{en}' (node {target}): {model.Nodes.Count} node(s), {model.Edges.Count} edge line(s) (ancestor subgraph) ---");

        foreach (var e in model.Edges)
        {
            string kind = e.IsDashed ? "lookup" : "value";
            Console.WriteLine($"    edge {e.FromIndex,2} -> {e.ToIndex,2}  wire={e.WireIndex}  [{kind}]  label='{e.Label}'");
        }

        foreach (var node in model.Nodes.OrderBy(n => n.Index))
        {
            string name2 = (node.Expression as BlockElement)?.ElementName ?? "";
            string inStr = string.Join(", ", node.InputPorts.Select(p => $"{p.Name}[{p.WireIndex}]"));
            string outStr = string.Join(", ", node.OutputPorts.Select(p => $"{p.Name}[{p.WireIndex}]"));
            Console.WriteLine($"    node {node.Index,2} ({node.Expression.GetType().Name}{(name2.Length > 0 ? $" '{name2}'" : "")})");
            Console.WriteLine($"        in:  [{inStr}]");
            Console.WriteLine($"        out: [{outStr}]");
        }
    }
}
