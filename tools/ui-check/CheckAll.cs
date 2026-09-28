using ACadSharp;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using System;
using System.IO;
using System.Linq;

class CheckAll
{
    const double ColumnWidth = 320;
    public static void Run(string file)
    {
        CadDocument doc = CadFileService.LoadFile(file);
        int anomalies = 0, total = 0;
        foreach (BlockRecord block in doc.BlockRecords)
        {
            EvaluationGraph? graph = block.EvaluationGraph;
            if (graph is null) { continue; }
            BlockModel? model = BlockModel.Create(block);
            if (model is null) { continue; }
            foreach (PropertyItem prop in model.Properties)
            {
                GraphModel.Result sub = GraphModel.BuildAncestors(graph, prop.NodeIndex);
                var pos = sub.Nodes.ToDictionary(n => n.Index, n => (sub.MaxDepth - n.Depth) * ColumnWidth);
                foreach (GraphEdgeInfo e in sub.Edges)
                {
                    if (!pos.TryGetValue(e.FromIndex, out double xf) || !pos.TryGetValue(e.ToIndex, out double xt)) { continue; }
                    total++;
                    if (xf > xt + 0.01)
                    {
                        // Provider to the RIGHT of the consumer: against the flow.
                        bool lookup = e.IsDashed;
                        anomalies++;
                        Console.WriteLine($"  [{(lookup ? "lookup" : "!! NON-LOOKUP !!")}] {Path.GetFileName(file)} / {block.Name} / '{prop.Name}': #{e.FromIndex} (x={xf:0}) -> #{e.ToIndex} (x={xt:0})");
                    }
                }
            }
        }
        Console.WriteLine($"{Path.GetFileName(file)}: {anomalies} of {total} edges go right-to-left");
    }
}
