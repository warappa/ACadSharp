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

    /// <summary>
    /// The value currently flowing through this port (read from the evaluation
    /// context under the port's raw name), or null when the value is unknown / unset.
    /// </summary>
    public string? ValueText { get; }

    public bool HasValue => this.ValueText is not null;

    public PortInfo(string name, int peerIndex, int wireIndex, string? valueText = null)
    {
        Name = name;
        PeerIndex = peerIndex;
        WireIndex = wireIndex;
        ValueText = valueText;
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

    /// <summary>
    /// The value currently flowing along this edge (the source node's output
    /// port, or the target's port for a lookup edge), or null when the value
    /// is unknown / unset. Baked into <see cref="Label"/> for display.
    /// </summary>
    public string? ValueText { get; }

    public bool HasValue => this.ValueText is not null;

    public GraphEdgeInfo(int fromIndex, int toIndex, string label, bool isDashed, bool isFeedback = false, int wireIndex = 0, int wireCount = 1, string? valueText = null)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
        Label = label;
        IsDashed = isDashed;
        IsFeedback = isFeedback;
        WireIndex = wireIndex;
        WireCount = wireCount;
        ValueText = valueText;
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
        // layout concern (GraphLayout.ComputeRows), so the model assigns only
        // the depth (the BFS distance, a graph property).
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
        // TrackedCount). The output port is named after the source's stored
        // port; the input port is named after the target's C# property (the
        // connection's semantic role), so the two ends of a connection carry
        // different names. A ×2 edge yields two ports (e.g. the X and Y of a
        // grip) and two parallel edge lines, so the "where is the second
        // value?" question is answered by the picture itself. The "reverse"
        // direction of an invertible pair is skipped (DAG).
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
            var (outputNames, inputNames, rawOutputNames) = GetPortNames(byIndex, edge);

            // The source's expression (object) id — the key the evaluation
            // context stores the source's written values under.
            int sourceExprId = GetExpression(byIndex, edge.FromNodeIndex)?.Id ?? -1;

            // One edge line + one port pair per wire. The output port keeps
            // the source's stored port name; the input port is named after
            // the target's C# property, so the two ends differ. The value
            // flowing on the wire is read from the evaluation context the
            // same way the engine reads it (context[sourceExprId][portName])
            // and is shared by the edge label and both port ends.
            for (int wire = 0; wire < outputNames.Count; wire++)
            {
                string? valueText = LookupValue(graph, sourceExprId, rawOutputNames[wire]);
                result.Edges.Add(BuildEdgeLabel(byIndex, edge, isFeedback, outputNames[wire], wire, outputNames.Count, valueText));
                outputPorts[edge.FromNodeIndex].Add(new PortInfo(outputNames[wire], edge.ToNodeIndex, wire, valueText));
                inputPorts[edge.ToNodeIndex].Add(new PortInfo(inputNames[wire], edge.FromNodeIndex, wire, valueText));
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
    /// The display names and the raw context keys for a logical edge's wires:
    /// one (output, input, rawOutput) triple per wire (<c>TrackedCount</c>).
    /// The <em>output</em> name is the <c>Name</c> field of the corresponding
    /// connection on the target node that references the source node (the
    /// source's stored port), humanized; the <em>input</em> name is the
    /// target's C# property that holds the connection (its semantic role),
    /// humanized with the "Connection" suffix stripped; the <em>rawOutput</em>
    /// name is that same stored port <em>unhumanized</em> — the exact key the
    /// source wrote its value to the evaluation context under, so the value
    /// flowing on the wire is looked up as <c>context[output.Source][rawOutput]</c>.
    /// The names are display-only (the stored value is untouched). Falls back
    /// to a type-specific default (indexed for the surplus wires) when fewer
    /// connections than wires are found (the EvalConnection entries are not
    /// always populated by the file reader); the input and the raw key then
    /// fall back to the same (fallback) name.
    /// </summary>
    private static (List<string> Output, List<string> Input, List<string> RawOutput) GetPortNames(Dictionary<int, EvaluationGraph.Node> byIndex, EvaluationGraph.Edge edge)
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

        // The connections on the target that reference the source: each with
        // its port name (output) and its C# property name (input).
        List<(string PortName, string PropertyName)> names = new();
        if (toExpr is not null && fromExpr is not null)
        {
            foreach ((int id, string portName, string propertyName) in GetConnections(toExpr))
            {
                if (id == fromExpr.Id)
                {
                    names.Add((portName, propertyName));
                }
            }
        }

        // Build exactly `count` (output, input, rawOutput) triples: use the
        // connection names in order, padding with indexed fallbacks when there
        // are fewer names than wires. `rawOutput[i]` is the unhumanized port
        // name — the exact key the value was written to the evaluation context
        // under. It equals the humanized name for the fallback cases (the
        // fallback is already a display string) and is the source's stored port
        // name for the real connections.
        List<string> output = new();
        List<string> input = new();
        List<string> rawOutput = new();
        for (int i = 0; i < count; i++)
        {
            if (i < names.Count)
            {
                rawOutput.Add(names[i].PortName);
                output.Add(Humanize(names[i].PortName));
                input.Add(Humanize(names[i].PropertyName));
            }
            else if (names.Count == 0)
            {
                // No explicit connections: one fallback for the first wire,
                // indexed for the rest.
                string f = i == 0 ? fallback : $"{fallback} {i + 1}";
                rawOutput.Add(f);
                output.Add(f);
                input.Add(f);
            }
            else
            {
                // Fewer names than wires: index the surplus wires.
                string f = $"{names[^1].PortName} {i + 1}";
                rawOutput.Add(f);
                output.Add(f);
                input.Add(f);
            }
        }

        return (output, input, rawOutput);
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
        int wireCount,
        string? valueText)
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

        // Append the value flowing on this wire ("name = value"), so the
        // edge reads as a data-flow trace: the port the value came through
        // plus the value itself (a lookup edge: 'lookup (lookup String) = "Custom"').
        if (valueText is not null)
        {
            label += $" = {valueText}";
        }

        return new GraphEdgeInfo(edge.FromNodeIndex, edge.ToNodeIndex, label, isLookup, isFeedback, wireIndex, wireCount, valueText);
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
    /// The value flowing along the wire from the source (expression id
    /// <paramref name="sourceExprId"/>) to the target under the raw port name
    /// <paramref name="rawName"/>.
    /// <para>
    /// This is exactly what the target reads at evaluation time
    /// (<see cref="EvaluationContext.TryGetValue"/> on
    /// <c>context[connection.Id][connection.Name]</c>, the engine's
    /// <c>ReadConnectionValue</c>): the source node writes its result to
    /// <c>context[sourceExprId][port]</c> (the context is keyed by the node's
    /// expression object id — the same id the file stores in the target's
    /// <c>EvalConnection</c>), so the value the target consumes is the value
    /// flowing on the wire. A lookup action's write-back (the matched cell)
    /// lands in the same location, so this also resolves the lookup result.
    /// Returns the formatted value, or null when the source never wrote the
    /// port (the engine's read then falls back to its default, so the wire
    /// carries no value to display).
    /// </para>
    /// </summary>
    private static string? LookupValue(EvaluationGraph graph, int sourceExprId, string rawName)
    {
        EvaluationContext context = graph.Context;
        if (context.TryGetValue(sourceExprId, rawName, out EvaluationValue value) && value.Type != EvaluationValueType.None)
        {
            return ValueFormatter.Format(value);
        }

        return null;
    }

    /// <summary>
    /// Collects all (targetId, portName, propertyName) connection entries of an
    /// expression (mirrors the pattern in ACadSharp.Examples). The
    /// <c>propertyName</c> is the C# property on the target that holds the
    /// connection (its semantic input role); for a lookup-table column there is
    /// no such property, so it equals the port name.
    /// </summary>
    private static List<(int Id, string PortName, string PropertyName)> GetConnections(EvaluationExpression expr)
    {
        List<(int Id, string PortName, string PropertyName)> result = new();
        void Add(EvalConnection? c, string property)
        {
            if (c is not null && c.Id != 0)
            {
                result.Add((c.Id, c.Name, property));
            }
        }
        void AddProp(EvalParameterProperty? p, string property)
        {
            if (p is null)
            {
                return;
            }

            foreach (EvalConnection c in p.Connections)
            {
                Add(c, property);
            }
        }
        switch (expr)
        {
            case BlockLookupAction a:
                foreach (BlockLookupAction.ColumnData col in a.Columns)
                {
                    if (col.NodeId != 0)
                    {
                        // No C# property behind a table column: the input name
                        // is the connection name.
                        result.Add((col.NodeId, col.ConnectionName, col.ConnectionName));
                    }
                }
                break;
            case BlockFlipParameter p:
                AddProp(p.FirstPointDisplacementX, nameof(p.FirstPointDisplacementX));
                AddProp(p.FirstPointDisplacementY, nameof(p.FirstPointDisplacementY));
                AddProp(p.SecondPointDisplacementX, nameof(p.SecondPointDisplacementX));
                AddProp(p.SecondPointDisplacementY, nameof(p.SecondPointDisplacementY));
                Add(p.UpdatedFlipConnection, nameof(p.UpdatedFlipConnection));
                break;
            case Block1PtParameter p:
                AddProp(p.DisplacementX, nameof(p.DisplacementX));
                AddProp(p.DisplacementY, nameof(p.DisplacementY));
                break;
            case Block2PtParameter p:
                AddProp(p.FirstPointDisplacementX, nameof(p.FirstPointDisplacementX));
                AddProp(p.FirstPointDisplacementY, nameof(p.FirstPointDisplacementY));
                AddProp(p.SecondPointDisplacementX, nameof(p.SecondPointDisplacementX));
                AddProp(p.SecondPointDisplacementY, nameof(p.SecondPointDisplacementY));
                break;
            case BlockGripLocationComponent c:
                Add(c.Connection, nameof(c.Connection));
                break;
            case BlockScaleAction a:
                Add(a.ScaleConnection, nameof(a.ScaleConnection));
                Add(a.XScaleConnection, nameof(a.XScaleConnection));
                Add(a.YScaleConnection, nameof(a.YScaleConnection));
                Add(a.UpdateBaseXConnection, nameof(a.UpdateBaseXConnection));
                Add(a.UpdateBaseYConnection, nameof(a.UpdateBaseYConnection));
                break;
            case BlockMoveAction a:
                Add(a.XDeltaConnection, nameof(a.XDeltaConnection));
                Add(a.YDeltaConnection, nameof(a.YDeltaConnection));
                break;
            case BlockRotationAction a:
                Add(a.AngleDeltaConnection, nameof(a.AngleDeltaConnection));
                Add(a.UpdateBaseXConnection, nameof(a.UpdateBaseXConnection));
                Add(a.UpdateBaseYConnection, nameof(a.UpdateBaseYConnection));
                break;
            case BlockStretchAction a:
                Add(a.EndXDeltaConnection, nameof(a.EndXDeltaConnection));
                Add(a.EndYDeltaConnection, nameof(a.EndYDeltaConnection));
                break;
            case BlockPolarStretchAction a:
                Add(a.BaseConnection, nameof(a.BaseConnection));
                Add(a.BaseXDeltaConnection, nameof(a.BaseXDeltaConnection));
                Add(a.BaseYDeltaConnection, nameof(a.BaseYDeltaConnection));
                Add(a.EndConnection, nameof(a.EndConnection));
                Add(a.UpdatedBaseConnection, nameof(a.UpdatedBaseConnection));
                Add(a.UpdatedEndConnection, nameof(a.UpdatedEndConnection));
                break;
            case BlockArrayAction a:
                Add(a.BaseConnection, nameof(a.BaseConnection));
                Add(a.EndConnection, nameof(a.EndConnection));
                Add(a.UpdatedBaseConnection, nameof(a.UpdatedBaseConnection));
                Add(a.UpdatedEndConnection, nameof(a.UpdatedEndConnection));
                break;
            case BlockFlipAction a:
                Add(a.FlipConnection, nameof(a.FlipConnection));
                Add(a.UpdatedBaseConnection, nameof(a.UpdatedBaseConnection));
                Add(a.UpdatedEndConnection, nameof(a.UpdatedEndConnection));
                Add(a.UpdatedFlipConnection, nameof(a.UpdatedFlipConnection));
                break;
        }
        return result;
    }

    /// <summary>
    /// Humanizes a PascalCase C# property name into a space-separated label
    /// (e.g. "AngleDeltaConnection" → "Angle Delta"): the "Connection" suffix
    /// is stripped (display only), and a space is inserted before an uppercase
    /// letter that follows a lowercase letter or is followed by one.
    /// </summary>
    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Strip the "Connection" suffix (display only); keep it when it is the
        // whole name (a property literally called "Connection").
        const string suffix = "Connection";
        if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - suffix.Length);
        }

        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) &&
                (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
