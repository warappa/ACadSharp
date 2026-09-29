using System;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// The hover tooltip: a small floating label that follows the pointer and
/// describes the hovered element (a node, a port, or an edge). Holds the
/// tooltip's XAML elements (the border + the text) and positions them. The
/// positions are relative to the tooltip's parent (the overlay canvas).
/// </summary>
public class GraphTooltip
{
    // The design language's quick-fade duration: 83ms. The hover tooltip fades
    // in and out over this. (A DispatcherTimer tween rather than an XAML
    // Transition: the headless capture environment does not drive the implicit
    // animation system, so a Transition on Opacity throws on instantiation.)
    private const int FadeSteps = 5;

    private readonly Border _tip;
    private readonly TextBlock _text;
    private DispatcherTimer? _fade;
    private double _fadeFrom;
    private double _fadeTo;
    private int _fadeStep;

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
    /// Hides the tooltip (fades out over 83ms).
    /// </summary>
    public void Hide()
    {
        FadeTo(0);
    }

    /// <summary>
    /// Moves the tooltip to follow the pointer (when it is showing).
    /// </summary>
    public void Move(Point at)
    {
        if (_tip.Opacity > 0)
        {
            Canvas.SetLeft(_tip, at.X + 14);
            Canvas.SetTop(_tip, at.Y + 14);
        }
    }

    private void Set(string text, Point at)
    {
        _text.Text = text;
        Canvas.SetLeft(_tip, at.X + 14);
        Canvas.SetTop(_tip, at.Y + 14);
        FadeTo(1);
    }

    /// <summary>
    /// Fades the tooltip's opacity to <paramref name="target"/> (0 or 1) over
    /// ~83ms, cancelling any in-flight fade. The element stays IsVisible: a
    /// transparent element is not painted and the overlay is not hit-testable,
    /// so there is nothing to hide.
    /// </summary>
    private void FadeTo(double target)
    {
        if (target == _tip.Opacity)
        {
            return;
        }

        _fade?.Stop();
        _fadeFrom = _tip.Opacity;
        _fadeTo = target;
        _fadeStep = 0;
        _fade ??= new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(16) // 5 ticks ≈ 80ms ≈ the 83ms target
        };
        _fade.Tick -= OnFadeTick;
        _fade.Tick += OnFadeTick;
        _fade.Start();
    }

    private void OnFadeTick(object? s, EventArgs e)
    {
        _fadeStep++;
        double t = Math.Min(1.0, _fadeStep / (double)FadeSteps);
        _tip.Opacity = _fadeFrom + (_fadeTo - _fadeFrom) * t;
        if (_fadeStep >= FadeSteps)
        {
            _tip.Opacity = _fadeTo;
            _fade?.Stop();
        }
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
