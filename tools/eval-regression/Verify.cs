using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using System;
using System.Linq;
using System.Collections.Generic;

// Forward + reverse evaluation regression harness: reads a DWG, and for every
// dynamic block activates all grips, runs Evaluate() and EvaluateReverse(),
// counts failures, and (for the named block of interest) dumps the
// topological order, the invertible (flag-4) edges, and the lookup
// actions' CurrentValue after each pass.
string file = args.Length > 0 ? args[0] : "samples/dynamic-blocks/BLOCKLOOKUPPARAMETER.dwg";
CadDocument doc;
using (DwgReader reader = new DwgReader(file))
{
    doc = reader.Read();
}

int dynamicCount = 0;
int fwdFailed = 0;
int revFailed = 0;

foreach (BlockRecord block in doc.BlockRecords)
{
    EvaluationGraph graph = block.EvaluationGraph;
    if (graph is null)
    {
        continue;
    }

    dynamicCount++;

    List<int> gripIndices = graph.Nodes
        .Where(n => n.Expression is BlockGrip)
        .Select(n => n.Index)
        .ToList();
    graph.Activate(gripIndices);

    bool fwd = graph.Evaluate();

    // Re-activate for the reverse pass (Activate replaces the activated set).
    graph.Activate(gripIndices);
    bool rev = graph.EvaluateReverse();

    if (!fwd) fwdFailed++;
    if (!rev) revFailed++;

    Console.WriteLine($"block '{block.Name}'  (nodes {graph.Nodes.Count()}, edges {graph.Edges.Count})");
    Console.WriteLine($"   forward  : {(fwd ? "OK" : "FAILED")}   reverse: {(rev ? "OK" : "FAILED")}");

    // Details for the block of interest.
    if (block.Name.Contains("Callout Bubble - Imperial"))
    {
        Console.WriteLine("   --- Callout Bubble - Imperial details ---");
        Console.WriteLine($"   activated grips: {string.Join(", ", gripIndices)}");

        List<int> fwdOrder = graph.GetTopologicalOrder(gripIndices);
        List<int> revOrder = graph.GetTopologicalOrder(gripIndices, reverse: true);
        Console.WriteLine($"   forward order : {string.Join(", ", fwdOrder)}");
        Console.WriteLine($"   reverse order : {string.Join(", ", revOrder)}");

        // Show the invertible (flag-4) pairs and their classification.
        foreach (EvaluationGraph.Edge e in graph.Edges)
        {
            if (e.Flags == EvaluationGraph.EdgeFlags.Invertible)
            {
                string kind = graph.IsReverseLookupEdge(e.Index) ? "REVERSE (action→param)" : "FORWARD (param→action)";
                Console.WriteLine($"   edge {e.Index}: node {e.FromNodeIndex} -> node {e.ToNodeIndex}  reverseEdge={e.ReverseEdge}  [{kind}]");
            }
        }

        // Show the lookup actions' CurrentValue after each pass.
        graph.Activate(gripIndices);
        graph.Evaluate();
        Console.WriteLine("   after forward pass:");
        foreach (EvaluationGraph.Node n in graph.Nodes.OrderBy(x => x.Index))
        {
            if (n.Expression is BlockLookupAction lookup)
            {
                Console.WriteLine($"      node {n.Index} BlockLookupAction#(id={lookup.Id}) CurrentValue = {lookup.CurrentValue} (type={lookup.CurrentValue.Type})");
            }
        }

        graph.Activate(gripIndices);
        graph.EvaluateReverse();
        Console.WriteLine("   after reverse pass:");
        foreach (EvaluationGraph.Node n in graph.Nodes.OrderBy(x => x.Index))
        {
            if (n.Expression is BlockLookupAction lookup)
            {
                Console.WriteLine($"      node {n.Index} BlockLookupAction#(id={lookup.Id}) CurrentValue = {lookup.CurrentValue} (type={lookup.CurrentValue.Type})");
            }
        }
    }
}

Console.WriteLine();
Console.WriteLine($"=== summary: {dynamicCount} dynamic block(s), forward failed {fwdFailed}, reverse failed {revFailed} ===");
