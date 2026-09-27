using System;
using System.Collections.Generic;
using System.Linq;
using ACadSharp.Objects.Evaluations;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// Shared layout metrics and color rules for the node graph and its mini-map.
/// Both <c>NodeGraphView</c> and <c>MiniMapView</c> draw the same layout
/// (columns by BFS depth, rows within a column), so the content bounds must be
/// identical for the mini-map's visible-area rectangle to line up. Keeping the
/// constants and the kind-color / box-height / accent rules in one place
/// guarantees the two views stay in sync.
/// </summary>
public static class GraphLayout
{
    public const double BoxWidth = 170;
    public const double BoxHeight = 48;

    // A named node shows three lines (name / type / value), so its box is
    // taller than the two-line boxes.
    public const double NamedBoxHeight = 64;

    // Column pitch: the horizontal gap between node boxes is ColumnWidth -
    // BoxWidth (150px). The edge labels sit in that gap (centered on the
    // line, 16px clear of the arrowhead), so the pitch must leave room for
    // the longest port-name label (e.g. "Displacement lookup ×2", ~120px).
    public const double ColumnWidth = 320;
    public const double RowHeight = 80;

    // Inset of the first/last column and row from the content edge.
    public const double LayoutMargin = 20;

    /// <summary>
    /// The height of a node's box: named nodes show three lines (name / type /
    /// value) and get the taller box; unnamed nodes stay at the two-line
    /// height.
    /// </summary>
    public static double BoxHeightFor(GraphNodeInfo? node)
    {
        string? name = (node?.Expression as BlockElement)?.ElementName;
        return string.IsNullOrWhiteSpace(name) ? BoxHeight : NamedBoxHeight;
    }

    /// <summary>
    /// The row of each node within its depth column, keyed by node index: the
    /// <c>GraphModel</c> adds nodes in depth-then-index order, so the row is a
    /// node's position among its same-depth peers. Computed in one pass (O(N)
    /// total) so <see cref="GetPosition"/> stays O(1) per node. Keeping this
    /// here (rather than storing it on the node) means the layout — not the
    /// data model — decides the row ordering.
    /// </summary>
    public static Dictionary<int, int> ComputeRows(GraphModel.Result model)
    {
        Dictionary<int, int> rows = new();
        Dictionary<int, int> counters = new();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            int count = counters.TryGetValue(node.Depth, out int c) ? c : 0;
            rows[node.Index] = count;
            counters[node.Depth] = count + 1;
        }
        return rows;
    }

    /// <summary>
    /// The top-left layout position of a node's box: the column is the BFS
    /// depth (the target, depth 0, sits in the rightmost column), the row
    /// comes from <see cref="ComputeRows"/> (precomputed, so this is O(1)
    /// per node). Single source of truth shared by the main view and the
    /// mini-map (their content bounds must match).
    /// </summary>
    public static Point GetPosition(GraphModel.Result model, GraphNodeInfo node, Dictionary<int, int> rows)
    {
        double x = (model.MaxDepth - node.Depth) * ColumnWidth + LayoutMargin;
        double y = rows[node.Index] * RowHeight + LayoutMargin;
        return new Point(x, y);
    }

    /// <summary>
    /// The natural content size (width × height) of the layout — the bounding
    /// box the transform fits to and the mini-map scales from. Must match the
    /// main view's content exactly.
    /// </summary>
    public static (double Width, double Height) GetContentSize(GraphModel.Result model)
    {
        double width = (model.MaxDepth + 1) * ColumnWidth + LayoutMargin * 2;
        double height = Math.Max(
            model.Nodes.GroupBy(n => n.Depth).Select(g => g.Count()).Max() * RowHeight + LayoutMargin * 2,
            300);
        return (width, height);
    }

    /// <summary>
    /// The fill color for a node box, by its color category.
    /// </summary>
    public static MediaColor GetKindColor(string kind) => kind switch
    {
        "Parameter" => MediaColor.Parse("#3D7EBF"),
        "Grip" => MediaColor.Parse("#3D9E5F"),
        "Action" => MediaColor.Parse("#C77B3D"),
        "Component" => MediaColor.Parse("#7A7A7A"),
        _ => MediaColor.Parse("#8E6FBF"),
    };

    /// <summary>
    /// The accent brush the user picked in the settings flyout (cached in
    /// <see cref="ThemeResources"/> and refreshed on theme/accent change).
    /// </summary>
    public static IBrush GetAccentBrush() => ThemeResources.Accent;
}
