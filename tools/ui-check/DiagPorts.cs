using ACadSharp;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using System;
using System.IO;

class DiagPorts
{
    public static void Run(string file)
    {
        CadDocument doc = CadFileService.LoadFile(file);
        foreach (BlockRecord block in doc.BlockRecords)
        {
            EvaluationGraph? graph = block.EvaluationGraph;
            if (graph is null) { continue; }
            Console.WriteLine($"Block: {block.Name}");
            foreach (EvaluationGraph.Edge edge in graph.Edges)
            {
                EvaluationExpression? from = GetExpr(graph, edge.FromNodeIndex);
                EvaluationExpression? to = GetExpr(graph, edge.ToNodeIndex);
                Console.WriteLine($"  edge #{edge.Index}: #{edge.FromNodeIndex} ({from?.GetType().Name}, Id={from?.Id}) -> #{edge.ToNodeIndex} ({to?.GetType().Name}, Id={to?.Id}) flags={edge.Flags}");
                if (to is not null)
                {
                    foreach (var conn in GetConns(to))
                    {
                        Console.WriteLine($"    conn: Id={conn.Id} Name='{conn.Name}'");
                    }
                }
            }
        }
    }

    static EvaluationExpression? GetExpr(EvaluationGraph graph, int idx)
    {
        foreach (var n in graph.Nodes)
            if (n.Index == idx) return n.Expression;
        return null;
    }

    static System.Collections.Generic.List<(int Id, string Name)> GetConns(EvaluationExpression expr)
    {
        var result = new System.Collections.Generic.List<(int, string)>();
        void Add(EvalConnection? c) { if (c is not null && c.Id != 0) result.Add((c.Id, c.Name)); }
        void AddProp(EvalParameterProperty? p) { if (p is null) return; foreach (var c in p.Connections) Add(c); }
        switch (expr)
        {
            case BlockLookupAction a:
                foreach (var col in a.Columns)
                    if (col.NodeId != 0) result.Add((col.NodeId, col.ConnectionName));
                break;
            case BlockFlipParameter p:
                AddProp(p.FirstPointDisplacementX); AddProp(p.FirstPointDisplacementY);
                AddProp(p.SecondPointDisplacementX); AddProp(p.SecondPointDisplacementY);
                Add(p.UpdatedFlipConnection); break;
            case Block1PtParameter p:
                AddProp(p.DisplacementX); AddProp(p.DisplacementY); break;
            case Block2PtParameter p:
                AddProp(p.FirstPointDisplacementX); AddProp(p.FirstPointDisplacementY);
                AddProp(p.SecondPointDisplacementX); AddProp(p.SecondPointDisplacementY); break;
            case BlockGripLocationComponent c: Add(c.Connection); break;
            case BlockScaleAction a:
                Add(a.ScaleConnection); Add(a.XScaleConnection); Add(a.YScaleConnection);
                Add(a.UpdateBaseXConnection); Add(a.UpdateBaseYConnection); break;
            case BlockMoveAction a: Add(a.XDeltaConnection); Add(a.YDeltaConnection); break;
            case BlockRotationAction a:
                Add(a.AngleDeltaConnection); Add(a.UpdateBaseXConnection); Add(a.UpdateBaseYConnection); break;
            case BlockStretchAction a: Add(a.EndXDeltaConnection); Add(a.EndYDeltaConnection); break;
            case BlockPolarStretchAction a:
                Add(a.BaseConnection); Add(a.BaseXDeltaConnection); Add(a.BaseYDeltaConnection);
                Add(a.EndConnection); Add(a.UpdatedBaseConnection); Add(a.UpdatedEndConnection); break;
            case BlockArrayAction a:
                Add(a.BaseConnection); Add(a.EndConnection);
                Add(a.UpdatedBaseConnection); Add(a.UpdatedEndConnection); break;
            case BlockFlipAction a:
                Add(a.FlipConnection); Add(a.UpdatedBaseConnection);
                Add(a.UpdatedEndConnection); Add(a.UpdatedFlipConnection); break;
        }
        return result;
    }
}
