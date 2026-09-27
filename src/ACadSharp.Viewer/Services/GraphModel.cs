using ACadSharp.Objects.Evaluations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// A named port (slot) on a node, connected to a specific peer node and wire.
/// An edge can carry multiple parallel "wires" (<c>TrackedCount</c>); each
/// wire is its own port here, so a ×2 edge shows two circles (e.g. the X and
/// Y of a grip) rather than one circle plus a "×2" badge.
/// </summary>
public class PortInfo
{
    public string Name { get; }
    public int PeerIndex { get; }

    /// <summary>
    /// The wire (0-based) within the edge this port belongs to.
    /// </summary>
    public int WireIndex { get; }

    public PortInfo(string name, int peerIndex, int wireIndex)
    {
        Name = name;
        PeerIndex = peerIndex;
        WireIndex = wireIndex;
    }
}

/// <summary>
/// A node in the ancestor subgraph, with its layout position
/// (column = BFS depth from the target, row = order within the column)
/// and its input/output ports.
/// </summary>
public class GraphNodeInfo
{
    public int Index { get; }
    public EvaluationExpression Expression { get; }
    public int Depth { get; }

    /// <summary>
    /// Color category: Parameter / Grip / Action / Component / Other.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Input ports (left side of the box): each entry is a port that reads
    /// from a specific peer node.
    /// </summary>
    public List<PortInfo> InputPorts { get; set; } = new();

    /// <summary>
    /// Output ports (right side of the box): each entry is a port that a
    /// specific peer node reads from.
    /// </summary>
    public List<PortInfo> OutputPorts { get; set; } = new();

    public GraphNodeInfo(int index, EvaluationExpression expression, int depth)
    {
        Index = index;
        Expression = expression;
        Depth = depth;
        Kind = expression switch
        {
            BlockParameter => "Parameter",
            BlockGrip => "Grip",
            BlockAction => "Action",
            BlockGripLocationComponent => "Component",
            _ => "Other",
        };
    }
}

/// <summary>
/// An edge in the ancestor subgraph, with its display label.
/// </summary>
public class GraphEdgeInfo
{
    public int FromIndex { get; }
    public int ToIndex { get; }
    public string Label { get; }

    /// <summary>
    /// True for lookup-related edges (flag 4 / lookup action connections),
    /// drawn dashed.
    /// </summary>
    public bool IsDashed { get; }

    /// <summary>
    /// True when the edge goes right-to-left (the source sits in a
    /// lower-depth column than the target), drawn as a distinct feedback
    /// arc arcing above the boxes.
    /// </summary>
    public bool IsFeedback { get; }

    /// <summary>
    /// The wire (0-based) within the logical edge this <c>GraphEdgeInfo</c>
    /// represents. A logical edge with <c>TrackedCount</c> = N produces N
    /// <c>GraphEdgeInfo</c> entries (wire 0..N-1), drawn as N parallel lines.
    /// </summary>
    public int WireIndex { get; }

    /// <summary>
    /// The total number of wires the logical edge carries (1 for a plain
    /// edge). Used to suppress the per-wire edge label when the edge fans
    /// out — the port labels (next to the circles) already name each wire,
    /// so a per-wire edge label would just crowd the gap between the nodes.
    /// </summary>
    public int WireCount { get; }

    public GraphEdgeInfo(int fromIndex, int toIndex, string label, bool isDashed, bool isFeedback = false, int wireIndex = 0, int wireCount = 1)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
        Label = label;
        IsDashed = isDashed;
        IsFeedback = isFeedback;
        WireIndex = wireIndex;
        WireCount = wireCount;
    }
}

/// <summary>
/// Builds the ancestor subgraph of a target node: the target plus the
/// transitive closure of its incoming edges (value sources).
///
/// This is a <em>view-model</em>, not a pure data model: it pre-computes the
/// display data the graph views render — the per-wire port names, the edge
/// labels, the feedback flag, and the lookup (dashed) flag — from the raw
/// <see cref="EvaluationGraph"/>. The <em>layout</em> (the column/row
/// positions, the box heights, the content bounds) is <em>not</em> here; it
/// lives in <see cref="GraphLayout"/>, which consumes the graph data (the
/// BFS depth) and turns it into positions. Lookup actions are co-located with
/// their parameters (reassigned to the mode depth of their flag-4-connected
/// neighbors) so the 2-cycle stays within one column; the residual
/// target→action edge is marked as feedback and drawn as a distinct arc.
/// </summary>
public static class GraphModel
{
    public class Result
    {
        public List<GraphNodeInfo> Nodes { get; } = new();
        public List<GraphEdgeInfo> Edges { get; } = new();
        public int MaxDepth { get; set; }
    }

    public static Result BuildAncestors(EvaluationGraph graph, int targetIndex)
    {
        // Node lookup by the Index (group 91) field.
        Dictionary<int, EvaluationGraph.Node> byIndex = graph.Nodes
            .Where(n => n.Expression is not null)
            .ToDictionary(n => n.Index);

        // First-visit BFS depth over incoming edges. The "reverse" direction
        // of a flag-4 (lookup) pair is skipped to keep the graph acyclic
        // (AutoCAD's AcDbEvalGraph is a DAG by design; the file stores both
        // directions of a bidirectional lookup link, but only one is a real
        // data-flow edge).
        Dictionary<int, int> depth = new() { [targetIndex] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(targetIndex);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int edgeIndex in graph.GetIncomingEdges(current))
            {
                if (graph.IsReverseLookupEdge(edgeIndex))
                {
                    continue;
                }

                int from = graph.Edges[edgeIndex].FromNodeIndex;
                if (!depth.ContainsKey(from))
                {
                    depth[from] = depth[current] + 1;
                    queue.Enqueue(from);
                }
            }
        }

        // Rows: within each column, order by node index.
        var byDepth = depth
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).OrderBy(i => i).ToList());

        int maxDepth = depth.Count == 0 ? 0 : depth.Values.Max();
        var result = new Result { MaxDepth = maxDepth };

        // Nodes are added in depth-then-index order; the row ordering is a
        // layout concern (GraphLayout.GetRow), so the model assigns only the
        // depth (the BFS distance, a graph property).
        foreach ((int d, List<int> indices) in byDepth)
        {
            foreach (int nodeIndex in indices)
            {
                if (!byIndex.TryGetValue(nodeIndex, out EvaluationGraph.Node? node) || node is null)
                {
                    continue;
                }

                result.Nodes.Add(new GraphNodeInfo(nodeIndex, node.Expression!, d));
            }
        }

        // Ports + edges: for each logical edge, one output port on the
        // source and one input port on the target, per wire (the edge's
        // TrackedCount). The port name is the name of the corresponding
        // connection on the target; a ×2 edge yields two ports (e.g. the X
        // and Y of a grip) and two parallel edge lines, so the "where is the
        // second value?" question is answered by the picture itself. The
        // "reverse" direction of an invertible pair is skipped (DAG).
        var inputPorts = new Dictionary<int, List<PortInfo>>();
        var outputPorts = new Dictionary<int, List<PortInfo>>();
        foreach (int nodeIndex in depth.Keys)
        {
            inputPorts[nodeIndex] = new List<PortInfo>();
            outputPorts[nodeIndex] = new List<PortInfo>();
        }

        for (int i = 0; i < graph.Edges.Count; i++)
        {
            if (graph.IsReverseLookupEdge(i))
            {
                continue;
            }
            EvaluationGraph.Edge edge = graph.Edges[i];
            if (!depth.ContainsKey(edge.FromNodeIndex) || !depth.ContainsKey(edge.ToNodeIndex))
            {
                continue;
            }

            // An edge goes right-to-left when the source is in a lower-depth
            // column (more to the right) than the target.
            bool isFeedback = depth[edge.FromNodeIndex] < depth[edge.ToNodeIndex];
            List<string> portNames = GetPortNames(byIndex, edge);

            // One edge line + one port pair per wire.
            for (int wire = 0; wire < portNames.Count; wire++)
            {
                result.Edges.Add(BuildEdgeLabel(byIndex, edge, isFeedback, portNames[wire], wire, portNames.Count));
                outputPorts[edge.FromNodeIndex].Add(new PortInfo(portNames[wire], edge.ToNodeIndex, wire));
                inputPorts[edge.ToNodeIndex].Add(new PortInfo(portNames[wire], edge.FromNodeIndex, wire));
            }
        }

        foreach (GraphNodeInfo node in result.Nodes)
        {
            node.InputPorts = inputPorts[node.Index];
            node.OutputPorts = outputPorts[node.Index];
        }

        return result;
    }

    /// <summary>
    /// The display names for a logical edge's wires: one name per wire
    /// (<c>TrackedCount</c>). Each name is the <c>Name</c> field of the
    /// corresponding connection on the target node that references the source
    /// node. Falls back to a type-specific default (indexed for the surplus
    /// wires) when fewer connections than wires are found (the EvalConnection
    /// entries are not always populated by the file reader).
    /// </summary>
    private static List<string> GetPortNames(Dictionary<int, EvaluationGraph.Node> byIndex, EvaluationGraph.Edge edge)
    {
        int count = Math.Max(1, edge.TrackedCount);

        EvaluationExpression? toExpr = GetExpression(byIndex, edge.ToNodeIndex);
        EvaluationExpression? fromExpr = GetExpression(byIndex, edge.FromNodeIndex);

        // The type-specific fallback name (the EvalConnection entries are not
        // always populated by the file reader).
        string fallback = toExpr is null ? "Value" : toExpr switch
        {
            BlockLookupAction => "Lookup",
            Block1PtParameter => "Displacement",   // covers BlockLookupParameter, BlockPointParameter
            Block2PtParameter => "Displacement",  // covers BlockLinearParameter
            BlockGripLocationComponent => "Location",
            BlockScaleAction => "Scale",
            BlockMoveAction => "Move",
            BlockRotationAction => "Rotate",
            BlockStretchAction => "Stretch",
            BlockPolarStretchAction => "PolarStretch",
            BlockArrayAction => "Array",
            BlockFlipAction => "Flip",
            _ => "Value",
        };

        // The names of the connections on the target that reference the source.
        List<string> names = new();
        if (toExpr is not null && fromExpr is not null)
        {
            foreach ((int id, string name) in GetConnections(toExpr))
            {
                if (id == fromExpr.Id)
                {
                    names.Add(name);
                }
            }
        }

        // Build exactly `count` display names: use the connection names in
        // order, padding with indexed fallbacks when there are fewer names
        // than wires.
        List<string> result = new();
        for (int i = 0; i < count; i++)
        {
            if (i < names.Count)
            {
                result.Add(names[i]);
            }
            else if (names.Count == 0)
            {
                // No explicit connections: one fallback for the first wire,
                // indexed for the rest.
                result.Add(i == 0 ? fallback : $"{fallback} {i + 1}");
            }
            else
            {
                // Fewer names than wires: index the surplus wires.
                result.Add($"{names[^1]} {i + 1}");
            }
        }

        return result;
    }

    /// <summary>
    /// Edge label for one wire: the port name of the connection on the TO
    /// element bound to the FROM element. Lookup edges (flag 4 or a lookup
    /// action/parameter) get a "lookup (name)" label and are drawn dashed. The
    /// ×N count is no longer appended — each wire is its own line, so the
    /// count is visible as the number of parallel lines / port circles.
    /// </summary>
    private static GraphEdgeInfo BuildEdgeLabel(
        Dictionary<int, EvaluationGraph.Node> byIndex,
        EvaluationGraph.Edge edge,
        bool isFeedback,
        string portName,
        int wireIndex,
        int wireCount)
    {
        EvaluationExpression? toExpr = GetExpression(byIndex, edge.ToNodeIndex);
        EvaluationExpression? fromExpr = GetExpression(byIndex, edge.FromNodeIndex);

        bool isLookup = edge.Flags == EvaluationGraph.EdgeFlags.Invertible
            || toExpr is BlockLookupParameter
            || fromExpr is BlockLookupParameter
            || toExpr is BlockLookupAction
            || fromExpr is BlockLookupAction;

        string label = isLookup
            ? (portName.Length == 0 ? "lookup" : $"lookup ({portName})")
            : portName;

        return new GraphEdgeInfo(edge.FromNodeIndex, edge.ToNodeIndex, label, isLookup, isFeedback, wireIndex, wireCount);
    }

    // O(1) lookup via the pre-built index (was a linear scan of graph.Nodes
    // per edge, i.e. O(N·E) overall for a graph with N nodes and E edges).
    private static EvaluationExpression? GetExpression(Dictionary<int, EvaluationGraph.Node> byIndex, int nodeIndex)
    {
        if (byIndex.TryGetValue(nodeIndex, out EvaluationGraph.Node? node) && node is not null)
        {
            return node.Expression;
        }

        return null;
    }

    /// <summary>
    /// Collects all (targetId, portName) connection entries of an expression
    /// (mirrors the pattern in ACadSharp.Examples).
    /// </summary>
    private static List<(int Id, string Name)> GetConnections(EvaluationExpression expr)
    {
        List<(int Id, string Name)> result = new();
        void Add(EvalConnection? c)
        {
            if (c is not null && c.Id != 0)
            {
                result.Add((c.Id, c.Name));
            }
        }
        void AddProp(EvalParameterProperty? p)
        {
            if (p is null)
            {
                return;
            }

            foreach (EvalConnection c in p.Connections)
            {
                Add(c);
            }
        }
        switch (expr)
        {
            case BlockLookupAction a:
                foreach (BlockLookupAction.ColumnData col in a.Columns)
                {
                    if (col.NodeId != 0)
                    {
                        result.Add((col.NodeId, col.ConnectionName));
                    }
                }
                break;
            case BlockFlipParameter p:
                AddProp(p.FirstPointDisplacementX);
                AddProp(p.FirstPointDisplacementY);
                AddProp(p.SecondPointDisplacementX);
                AddProp(p.SecondPointDisplacementY);
                Add(p.UpdatedFlipConnection);
                break;
            case Block1PtParameter p:
                AddProp(p.DisplacementX);
                AddProp(p.DisplacementY);
                break;
            case Block2PtParameter p:
                AddProp(p.FirstPointDisplacementX);
                AddProp(p.FirstPointDisplacementY);
                AddProp(p.SecondPointDisplacementX);
                AddProp(p.SecondPointDisplacementY);
                break;
            case BlockGripLocationComponent c:
                Add(c.Connection);
                break;
            case BlockScaleAction a:
                Add(a.ScaleConnection);
                Add(a.XScaleConnection);
                Add(a.YScaleConnection);
                Add(a.UpdateBaseXConnection);
                Add(a.UpdateBaseYConnection);
                break;
            case BlockMoveAction a:
                Add(a.XDeltaConnection);
                Add(a.YDeltaConnection);
                break;
            case BlockRotationAction a:
                Add(a.AngleDeltaConnection);
                Add(a.UpdateBaseXConnection);
                Add(a.UpdateBaseYConnection);
                break;
            case BlockStretchAction a:
                Add(a.EndXDeltaConnection);
                Add(a.EndYDeltaConnection);
                break;
            case BlockPolarStretchAction a:
                Add(a.BaseConnection);
                Add(a.BaseXDeltaConnection);
                Add(a.BaseYDeltaConnection);
                Add(a.EndConnection);
                Add(a.UpdatedBaseConnection);
                Add(a.UpdatedEndConnection);
                break;
            case BlockArrayAction a:
                Add(a.BaseConnection);
                Add(a.EndConnection);
                Add(a.UpdatedBaseConnection);
                Add(a.UpdatedEndConnection);
                break;
            case BlockFlipAction a:
                Add(a.FlipConnection);
                Add(a.UpdatedBaseConnection);
                Add(a.UpdatedEndConnection);
                Add(a.UpdatedFlipConnection);
                break;
        }
        return result;
    }
}
