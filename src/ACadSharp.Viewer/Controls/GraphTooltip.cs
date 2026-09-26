using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// The hover tooltip: a small floating label that follows the pointer and
/// describes the hovered element (a node, a port, or an edge). Holds the
/// tooltip's XAML elements (the border + the text) and positions them. The
/// positions are relative to the tooltip's parent (the overlay canvas).
/// </summary>
public class GraphTooltip
{
    private readonly Border _tip;
    private readonly TextBlock _text;

    public GraphTooltip(Border tip, TextBlock text)
    {
        _tip = tip;
        _text = text;
    }

    /// <summary>
    /// Shows the tooltip for a node (its name, kind, and current values).
    /// </summary>
    public void ShowNode(GraphNodeInfo node, Point at)
    {
        Set(GraphDrawing.BuildTooltip(node), at);
    }

    /// <summary>
    /// Shows the tooltip for an edge: the connection between two nodes, its
    /// label, and its kind.
    /// </summary>
    public void ShowEdge(GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode, Point at)
    {
        // The node's name (when it has one) leads the label, then the kind
        // and index.
        string tip = $"{NodeLabel(fromNode)}  →  {NodeLabel(toNode)}";
        tip += edge.Label.Length > 0 ? $"\nport: {edge.Label}" : "\n(unlabeled connection)";
        if (edge.IsDashed)
        {
            tip += "\n(lookup connection)";
        }

        Set(tip, at);
    }

    /// <summary>
    /// Shows the tooltip for a port (its name and slot).
    /// </summary>
    public void ShowPort(PortInfo port, bool isInput, Point at)
    {
        string side = isInput ? "input" : "output";
        Set($"port: {port.Name}\n({side} slot)", at);
    }

    /// <summary>
    /// Hides the tooltip.
    /// </summary>
    public void Hide()
    {
        _tip.IsVisible = false;
    }

    /// <summary>
    /// Moves the tooltip to follow the pointer (when it is visible).
    /// </summary>
    public void Move(Point at)
    {
        if (_tip.IsVisible)
        {
            Canvas.SetLeft(_tip, at.X + 14);
            Canvas.SetTop(_tip, at.Y + 14);
        }
    }

    private void Set(string text, Point at)
    {
        _text.Text = text;
        _tip.IsVisible = true;
        Canvas.SetLeft(_tip, at.X + 14);
        Canvas.SetTop(_tip, at.Y + 14);
    }

    private static string NodeLabel(GraphNodeInfo? node)
    {
        if (node is null)
        {
            return "?";
        }

        string? name = GraphDrawing.GetName(node.Expression);
        return name is null
            ? $"{node.Kind} #{node.Index}"
            : $"{name} ({node.Kind}) #{node.Index}";
    }
}
