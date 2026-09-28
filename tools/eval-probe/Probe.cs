using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using CSMath;

// Inspect the evaluation graph of every dynamic block in a DXF file:
// activates all grips, evaluates, then dumps each node's type, its
// CurrentValue (or <unset>), and — for parameters — label/description/
// ElementName/stored location.
var f = args.Length > 0 ? args[0] : "samples/dynamic-blocks/BLOCKLOOKUPPARAMETER.dxf";
CadDocument doc = new DxfReader(f).Read();
foreach (var block in doc.BlockRecords)
{
    var graph = block.EvaluationGraph;
    if (graph is null) continue;
    var grips = graph.Nodes.Where(n => n.Expression is BlockGrip).Select(n => n.Index).ToList();
    graph.Activate(grips);
    graph.Evaluate();
    Console.WriteLine($"=== block {block.Name} ===");
    foreach (var node in graph.Nodes.OrderBy(n => n.Index))
    {
        var e = node.Expression;
        if (e is null) continue;
        string extra = "";
        if (e is BlockParameter p)
        {
            (string label, string desc) = p switch
            {
                BlockLinearParameter l => (l.Label, l.Description),
                BlockPointParameter pt => (pt.Label, pt.Description),
                BlockPolarParameter po => (po.Label, po.Description),
                BlockRotationParameter r => (r.Label, r.Description),
                BlockFlipParameter fl => (fl.Label, fl.Description),
                BlockVisibilityParameter v => (v.Label, v.Description),
                BlockLookupParameter lk => (lk.Label, lk.Description),
                BlockXYParameter xy => (xy.LabelX + "/" + xy.LabelY, xy.DescriptionX + "/" + xy.DescriptionY),
                _ => ("?", "?"),
            };
            extra = $" Label=[{label}] Desc=[{desc}] ElementName=[{p.ElementName}]";
            if (p is Block1PtParameter one) extra += $" Location={one.Location}";
        }
        Console.WriteLine($"  node {node.Index,2}: {e.GetType().Name,-26} value={ValueStr(e)}{extra}");
    }
}
static string ValueStr(EvaluationExpression e)
{
    var v = e.CurrentValue;
    if (v.Type == EvaluationValueType.None) return "<unset>";
    if (v.PointValue is { } p) return $"point({p.X:0.##}, {p.Y:0.##})";
    return v.ToString();
}
