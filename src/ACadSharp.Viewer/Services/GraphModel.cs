using ACadSharp.Objects.Evaluations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// A named port (slot) on a node, connected to a specific peer node.
/// </summary>
public class PortInfo
{
    public string Name { get; }
    public int PeerIndex { get; }

    public PortInfo(string name, int peerIndex)
    {
        Name = name;
        PeerIndex = peerIndex;
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
    public int Row { get; }

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

    public GraphNodeInfo(int index, EvaluationExpression expression, int depth, int row)
    {
        Index = index;
        Expression = expression;
        Depth = depth;
        Row = row;
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

    public GraphEdgeInfo(int fromIndex, int toIndex, string label, bool isDashed, bool isFeedback = false)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
        Label = label;
        IsDashed = isDashed;
        IsFeedback = isFeedback;
    }
}

/// <summary>
/// Builds the ancestor subgraph of a target node: the target plus the
/// transitive closure of its incoming edges (value sources). Layout is
/// layered — the column is the first-visit BFS depth from the target,
/// the target sits in the rightmost column and values flow left to right.
/// Lookup actions are co-located with their parameters (reassigned to the
/// mode depth of their flag-4-connected neighbors) so the 2-cycle stays
/// within one column; the residual target→action edge is marked as feedback
/// and drawn as a distinct arc.
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

        // First-visit BFS depth over incoming edges.
        Dictionary<int, int> depth = new() { [targetIndex] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(targetIndex);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int edgeIndex in graph.GetIncomingEdges(current))
            {
                int from = graph.Edges[edgeIndex].FromNodeIndex;
                if (!depth.ContainsKey(from))
                {
                    depth[from] = depth[current] + 1;
                    queue.Enqueue(from);
                }
            }
        }

        // Co-locate lookup actions with their parameters: reassign each
        // BlockLookupAction's depth to the mode depth of its flag-4-connected
        // neighbors. This keeps the action↔parameter 2-cycle within one
        // column and leaves only the target→action edge going right-to-left
        // (drawn as a feedback arc).
        foreach (KeyValuePair<int, int> kv in depth.ToList())
        {
            int nodeIndex = kv.Key;
            if (!byIndex.TryGetValue(nodeIndex, out EvaluationGraph.Node? node)
                || node.Expression is not BlockLookupAction)
            {
                continue;
            }

            List<int> neighborDepths = new();
            foreach (int edgeIdx in graph.GetOutgoingEdges(nodeIndex))
            {
                EvaluationGraph.Edge e = graph.Edges[edgeIdx];
                if ((e.Flags & 4) != 0 && depth.ContainsKey(e.ToNodeIndex))
                {
                    neighborDepths.Add(depth[e.ToNodeIndex]);
                }
            }
            foreach (int edgeIdx in graph.GetIncomingEdges(nodeIndex))
            {
                EvaluationGraph.Edge e = graph.Edges[edgeIdx];
                if ((e.Flags & 4) != 0 && depth.ContainsKey(e.FromNodeIndex))
                {
                    neighborDepths.Add(depth[e.FromNodeIndex]);
                }
            }

            if (neighborDepths.Count == 0)
            {
                continue;
            }

            depth[nodeIndex] = neighborDepths
                .GroupBy(d => d)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First().Key;
        }

        // Rows: within each column, order by node index.
        var byDepth = depth
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).OrderBy(i => i).ToList());

        int maxDepth = depth.Count == 0 ? 0 : depth.Values.Max();
        var result = new Result { MaxDepth = maxDepth };

        foreach ((int d, List<int> indices) in byDepth)
        {
            for (int row = 0; row < indices.Count; row++)
            {
                int nodeIndex = indices[row];
                if (!byIndex.TryGetValue(nodeIndex, out EvaluationGraph.Node node))
                {
                    continue;
                }

                result.Nodes.Add(new GraphNodeInfo(nodeIndex, node.Expression!, d, row));
            }
        }

        // Edges: every edge whose both ends are in the subgraph.
        foreach (EvaluationGraph.Edge edge in graph.Edges)
        {
            if (!depth.ContainsKey(edge.FromNodeIndex) || !depth.ContainsKey(edge.ToNodeIndex))
            {
                continue;
            }

            // An edge goes right-to-left when the source is in a lower-depth
            // column (more to the right) than the target.
            bool isFeedback = depth[edge.FromNodeIndex] < depth[edge.ToNodeIndex];
            result.Edges.Add(BuildEdgeLabel(graph, edge, isFeedback));
        }

        // Ports: for each edge, the port name (from the target's connection)
        // is an output port on the source and an input port on the target.
        var inputPorts = new Dictionary<int, List<PortInfo>>();
        var outputPorts = new Dictionary<int, List<PortInfo>>();
        foreach (int nodeIndex in depth.Keys)
        {
            inputPorts[nodeIndex] = new List<PortInfo>();
            outputPorts[nodeIndex] = new List<PortInfo>();
        }

        foreach (EvaluationGraph.Edge edge in graph.Edges)
        {
            if (!depth.ContainsKey(edge.FromNodeIndex) || !depth.ContainsKey(edge.ToNodeIndex))
            {
                continue;
            }

            string portName = GetPortName(graph, edge);
            outputPorts[edge.FromNodeIndex].Add(new PortInfo(portName, edge.ToNodeIndex));
            inputPorts[edge.ToNodeIndex].Add(new PortInfo(portName, edge.FromNodeIndex));
        }

        foreach (GraphNodeInfo node in result.Nodes)
        {
            node.InputPorts = inputPorts[node.Index];
            node.OutputPorts = outputPorts[node.Index];
        }

        return result;
    }

    /// <summary>
    /// Edge label: the port name of the connection on the TO element bound to
    /// the FROM element, plus a ×N count when the edge tracks multiple
    /// connections. Lookup edges (flag 4 or a lookup action/parameter) get a
    /// "lookup ×N" label and are drawn dashed.
    /// </summary>
    /// <summary>
    /// The port name for an edge: the <c>Name</c> field of the connection on
    /// the target node that references the source node. Empty when no such
    /// connection exists.
    /// </summary>
    private static string GetPortName(EvaluationGraph graph, EvaluationGraph.Edge edge)
    {
        EvaluationExpression? toExpr = GetExpression(graph, edge.ToNodeIndex);
        EvaluationExpression? fromExpr = GetExpression(graph, edge.FromNodeIndex);

        if (toExpr is null || fromExpr is null)
        {
            return string.Empty;
        }

        foreach ((int id, string name) in GetConnections(toExpr))
        {
            if (id == fromExpr.Id)
            {
                return name;
            }
        }

        return string.Empty;
    }

    private static GraphEdgeInfo BuildEdgeLabel(EvaluationGraph graph, EvaluationGraph.Edge edge, bool isFeedback)
    {
        EvaluationExpression? toExpr = GetExpression(graph, edge.ToNodeIndex);
        EvaluationExpression? fromExpr = GetExpression(graph, edge.FromNodeIndex);

        bool isLookup = (edge.Flags & 4) != 0
            || toExpr is BlockLookupParameter
            || fromExpr is BlockLookupParameter
            || toExpr is BlockLookupAction
            || fromExpr is BlockLookupAction;

        string portName = GetPortName(graph, edge);

        string label;
        if (isLookup)
        {
            label = portName.Length == 0
                ? "lookup"
                : $"lookup ({portName})";
        }
        else
        {
            label = portName;
        }

        if (edge.TrackedCount > 1)
        {
            label = label.Length == 0 ? $"×{edge.TrackedCount}" : $"{label} ×{edge.TrackedCount}";
        }

        return new GraphEdgeInfo(edge.FromNodeIndex, edge.ToNodeIndex, label, isLookup, isFeedback);
    }

    private static EvaluationExpression? GetExpression(EvaluationGraph graph, int nodeIndex)
    {
        foreach (EvaluationGraph.Node node in graph.Nodes)
        {
            if (node.Index == nodeIndex)
            {
                return node.Expression;
            }
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
