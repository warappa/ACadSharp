using ACadSharp;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using System;
using System.IO;
using System.Linq;
using ACadSharp.Viewer.Services;

class DumpGraph
{
    public static void Run(string file)
    {
        CadDocument doc = CadFileService.LoadFile(file);
        foreach (BlockRecord block in doc.BlockRecords)
        {
            EvaluationGraph? graph = block.EvaluationGraph;
            if (graph is null) { continue; }
            Console.WriteLine($"=== {Path.GetFileName(file)} / {block.Name} ===");
            foreach (EvaluationGraph.Node node in graph.Nodes)
            {
                if (node.Expression is null) { continue; }
                string name = (node.Expression as BlockElement)?.ElementName ?? "-";
                Console.WriteLine($"  node #{node.Index,-3} {node.Expression.GetType().Name,-28} name='{name}'  in={string.Join(",", node.GetIncomingEdges().Select(e => e.FromNodeIndex))}  out={string.Join(",", node.GetOutgoingEdges().Select(e => e.ToNodeIndex))}");
            }
            Console.WriteLine("  edges:");
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var e = graph.Edges[i];
                Console.WriteLine($"    edge #{i,-3} {e.FromNodeIndex} -> {e.ToNodeIndex}  flags={e.Flags}  tracked={e.TrackedCount}");
            }
        }
    }
}
