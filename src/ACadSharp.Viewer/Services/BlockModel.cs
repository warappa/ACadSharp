using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// One row in the properties table of a dynamic block: a block parameter
/// (its label, description, and evaluated value).
/// </summary>
public class PropertyItem
{
    public string Name { get; }
    public string Description { get; }
    public string ValueText { get; }
    public string TypeText { get; }
    public string ValueOnlyText => ValueFormatter.FormatValueOnly(Expression);
    public bool HasValue { get; }
    public int NodeIndex { get; }
    public EvaluationExpression Expression { get; }

    /// <summary>
    /// The nodes this node reads from (its <em>inputs</em>: the source of each incoming
    /// edge), as a comma-separated list; empty when the node has no incoming edges.
    /// </summary>
    public string InputsText { get; }

    /// <summary>
    /// The nodes this node writes to (its <em>outputs</em>: the target of each outgoing
    /// edge), as a comma-separated list; empty when the node has no outgoing edges.
    /// </summary>
    public string OutputsText { get; }

    public PropertyItem(EvaluationGraph.Node node, BlockParameter parameter)
    {
        (string name, string description) = GetLabelAndDescription(parameter);
        Name = name;
        Description = description;
        ValueText = ValueFormatter.Format(node.Expression);
        TypeText = ValueFormatter.TypeText(node.Expression);
        HasValue = node.Expression.CurrentValue.Type != EvaluationValueType.None;
        NodeIndex = node.Index;
        Expression = node.Expression;
        InputsText = JoinNodeNames(node.GetIncomingEdges(), edge => edge.FromNodeIndex, node.Graph);
        OutputsText = JoinNodeNames(node.GetOutgoingEdges(), edge => edge.ToNodeIndex, node.Graph);
    }

    /// <summary>
    /// Resolves each edge's connected node to its display name and joins them with commas.
    /// </summary>
    private static string JoinNodeNames(
        IEnumerable<EvaluationGraph.Edge> edges,
        Func<EvaluationGraph.Edge, int> nodeIndexSelector,
        EvaluationGraph graph)
    {
        List<string> names = new();
        foreach (EvaluationGraph.Edge edge in edges)
        {
            names.Add(graph.GetNodeDisplayName(nodeIndexSelector(edge)));
        }

        return string.Join(", ", names);
    }

    /// <summary>
    /// Per-class label/description extraction. Fallback chain: the class's
    /// Label/Description properties, then a type-derived name, then the
    /// element name (group code 300).
    /// </summary>
    private static (string Name, string Description) GetLabelAndDescription(BlockParameter p)
    {
        string fallbackName = p switch
        {
            BlockLinearParameter => "Linear",
            BlockPointParameter => "Point",
            BlockPolarParameter => "Polar",
            BlockRotationParameter => "Rotation",
            BlockFlipParameter => "Flip",
            BlockVisibilityParameter => "Visibility",
            BlockLookupParameter => "Lookup",
            BlockXYParameter => "X/Y",
            BlockBasePointParameter => "Base Point",
            BlockAlignmentParameter => "Alignment",
            _ => p.GetType().Name,
        };

        string name = p switch
        {
            BlockPolarParameter polar => polar.Label,
            BlockXYParameter xy => JoinParts(xy.LabelX, xy.LabelY),
            BlockLinearParameter linear => linear.Label,
            BlockPointParameter point => point.Label,
            BlockRotationParameter rotation => rotation.Label,
            BlockFlipParameter flip => flip.Label,
            BlockVisibilityParameter visibility => visibility.Label,
            BlockLookupParameter lookup => lookup.Label,
            _ => null,
        };

        string description = p switch
        {
            BlockPolarParameter polar => polar.Description,
            BlockXYParameter xy => JoinParts(xy.DescriptionX, xy.DescriptionY),
            BlockLinearParameter linear => linear.Description,
            BlockPointParameter point => point.Description,
            BlockRotationParameter rotation => rotation.Description,
            BlockFlipParameter flip => flip.Description,
            BlockVisibilityParameter visibility => visibility.Description,
            BlockLookupParameter lookup => lookup.Description,
            _ => null,
        };

        // Fallback chain for the name: label → type-derived name → element name (300).
        if (string.IsNullOrWhiteSpace(name))
        {
            name = !string.IsNullOrWhiteSpace(p.ElementName) ? p.ElementName : fallbackName;
        }

        // For classes without a description, use the element name as the description.
        if (string.IsNullOrWhiteSpace(description) && p is BlockBasePointParameter or BlockAlignmentParameter)
        {
            description = p.ElementName ?? string.Empty;
        }

        return (name, description ?? string.Empty);
    }

    private static string JoinParts(string? a, string? b)
    {
        string x = string.IsNullOrWhiteSpace(a) ? string.Empty : a!;
        string y = string.IsNullOrWhiteSpace(b) ? string.Empty : b!;
        if (x.Length == 0)
        {
            return y;
        }

        if (y.Length == 0)
        {
            return x;
        }

        return $"{x} / {y}";
    }
}

/// <summary>
/// The evaluated properties of a dynamic block. Activates all grip nodes
/// (initial state, zero displacement) and evaluates the graph once.
/// </summary>
public class BlockModel
{
    public BlockRecord Block { get; }
    public List<PropertyItem> Properties { get; }
    public bool EvaluationOk { get; }
    public int GripCount { get; }

    private BlockModel(BlockRecord block, List<PropertyItem> properties, bool evaluationOk, int gripCount)
    {
        Block = block;
        Properties = properties;
        EvaluationOk = evaluationOk;
        GripCount = gripCount;
    }

    /// <summary>
    /// Builds the model for a block with an evaluation graph, or null when
    /// the block has no graph. A failed evaluation still yields a model
    /// (with EvaluationOk = false) so the UI can show the error state.
    /// </summary>
    public static BlockModel? Create(BlockRecord block)
    {
        EvaluationGraph? graph = block.EvaluationGraph;
        if (graph is null)
        {
            return null;
        }

        List<int> gripIndices = graph.Nodes
            .Where(n => n.Expression is BlockGrip)
            .Select(n => n.Index)
            .ToList();
        graph.Activate(gripIndices);
        bool ok = graph.Evaluate();

        List<PropertyItem> properties = new();
        foreach (EvaluationGraph.Node node in graph.Nodes.OrderBy(n => n.Index))
        {
            if (node.Expression is BlockParameter parameter)
            {
                properties.Add(new PropertyItem(node, parameter));
            }
        }

        return new BlockModel(block, properties, ok, gripIndices.Count);
    }
}
