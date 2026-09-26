using ACadSharp.Objects.Evaluations;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// A small overview of the whole node graph, drawn at a fixed scale that
/// fits the graph's natural bounds (it never zooms). A highlighted
/// rectangle shows the main view's visible area (updated through
/// <see cref="SetViewRect"/>); pressing or dragging the mini-map raises
/// <see cref="Navigate"/> with the content point under the pointer, so the
/// host can re-center the main view there.
/// </summary>
public partial class MiniMapView : UserControl
{
    // Layout metrics (box / column / row sizes and the content margin) and the
    // kind-color / box-height / accent rules live in the shared GraphLayout
    // (also used by NodeGraphView) so the mini-map's content bounds stay
    // identical to the main view's.
    private static readonly IBrush EdgeLineBrush = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A));
    private static readonly IBrush WhiteBrush = new SolidColorBrush(MediaColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush ViewRectFill = new SolidColorBrush(MediaColor.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

    // Content -> mini-map mapping: point = offset + content * scale.
    private double _scale;
    private double _offsetX;
    private double _offsetY;
    private double _contentW;
    private double _contentH;

    private GraphModel.Result? _lastModel;
    private Rect? _lastViewRect;
    private Rectangle? _viewRect;
    private bool _navigating;
    // Per-node drag offsets (content coordinates), mirrored from the main
    // view through SetNodeOffset so the overview follows the moved nodes.
    private readonly Dictionary<int, Vector> _nodeOffsets = new();

    /// <summary>
    /// Raised with a content point (the main view's coordinate system) when
    /// the user presses or drags the mini-map; the host re-centers the main
    /// view on the point.
    /// </summary>
    public Action<Point>? Navigate { get; set; }

    public MiniMapView()
    {
        InitializeComponent();

        // A slightly elevated surface (when the theme defines one), falling
        // back to the base canvas color, then a fixed dark color.
        Root.Background = GetCardBrush();

        // The canvas has no intrinsic size (a Canvas desires 0x0); it is
        // arranged to the border's content area by the layout pass, so the
        // scale is (re)computed once the bounds are known.
        MiniCanvas.SizeChanged += (_, _) =>
        {
            if (_lastModel is not null)
            {
                LayoutGraph();
            }
        };

        // Pressing or dragging the mini-map navigates the main view (the
        // host wires Navigate to a center-on-point).
        Root.PointerPressed += OnPointerPressed;
        Root.PointerMoved += OnPointerMoved;
        Root.PointerReleased += OnPointerReleased;
        Root.PointerCaptureLost += OnPointerCaptureLost;
    }

    /// <summary>
    /// Draws the whole graph (nodes as kind-colored boxes, edges as thin
    /// lines) at the scale that fits the graph's natural bounds.
    /// </summary>
    public void SetGraph(GraphModel.Result model)
    {
        _lastModel = model;
        _nodeOffsets.Clear();
        LayoutGraph();
    }

    /// <summary>
    /// Updates a node's drag offset (content coordinates) and redraws the
    /// overview so the node's box (and its edges) follow the moved node.
    /// </summary>
    public void SetNodeOffset(int nodeIndex, Vector offset)
    {
        _nodeOffsets[nodeIndex] = offset;
        if (_lastModel is not null)
        {
            LayoutGraph();
        }
    }

    /// <summary>
    /// Positions the visible-area rectangle for the given rectangle in the
    /// main view's content coordinates. The rectangle is intersected with
    /// the content bounds: when the visible area is larger than the whole
    /// graph the border sits at the content edge, and when the view is
    /// panned entirely off the graph the indicator is hidden.
    /// </summary>
    public void SetViewRect(Rect rect)
    {
        _lastViewRect = rect;
        ApplyViewRect();
    }

    private void LayoutGraph()
    {
        MiniCanvas.Children.Clear();
        _viewRect = null;

        GraphModel.Result? model = _lastModel;
        if (model is null || model.Nodes.Count == 0)
        {
            _scale = 0;
            return;
        }

        // The content bounds must match NodeGraphView's natural size, so
        // the visible-area rectangle (computed in the main view's content
        // coordinates) maps onto the mini-map correctly.
        _contentW = (model.MaxDepth + 1) * GraphLayout.ColumnWidth + GraphLayout.LayoutMargin * 2;
        _contentH = Math.Max(
            model.Nodes.GroupBy(n => n.Depth).Select(g => g.Count()).Max() * GraphLayout.RowHeight + GraphLayout.LayoutMargin * 2,
            300);

        double w = Math.Max(1, MiniCanvas.Bounds.Width);
        double h = Math.Max(1, MiniCanvas.Bounds.Height);
        _scale = Math.Min(w / _contentW, h / _contentH);
        _offsetX = (w - _contentW * _scale) / 2;
        _offsetY = (h - _contentH * _scale) / 2;

        // Node positions (the same layout as the main view, plus the
        // user-drag offset so a dragged node follows in the overview).
        Dictionary<int, (Point pos, double height, string kind, bool isTarget)> positions = new();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            Vector off = _nodeOffsets.GetValueOrDefault(node.Index);
            double x = (model.MaxDepth - node.Depth) * GraphLayout.ColumnWidth + GraphLayout.LayoutMargin + off.X;
            double y = node.Row * GraphLayout.RowHeight + GraphLayout.LayoutMargin + off.Y;
            positions[node.Index] = (new Point(x, y), GraphLayout.BoxHeightFor(node), node.Kind, node.Depth == 0);
        }

        // Edges first (below the boxes): a straight line between the port
        // positions (the bezier curvature is indistinguishable at this
        // scale).
        foreach (GraphEdgeInfo edge in model.Edges)
        {
            if (!positions.TryGetValue(edge.FromIndex, out (Point pos, double height, string kind, bool isTarget) from)
                || !positions.TryGetValue(edge.ToIndex, out var to))
            {
                continue;
            }

            var line = new Line
            {
                StartPoint = Map(new Point(from.pos.X + GraphLayout.BoxWidth, from.pos.Y + from.height / 2)),
                EndPoint = Map(new Point(to.pos.X, to.pos.Y + to.height / 2)),
                Stroke = EdgeLineBrush,
                StrokeThickness = 1,
            };
            MiniCanvas.Children.Add(line);
        }

        // Node boxes (kind-colored; the target gets a white ring).
        foreach ((int index, (Point pos, double height, string kind, bool isTarget)) in positions)
        {
            Point at = Map(pos);
            var box = new Rectangle
            {
                Width = GraphLayout.BoxWidth * _scale,
                Height = height * _scale,
                Fill = new SolidColorBrush(GraphLayout.GetKindColor(kind)),
            };
            if (isTarget)
            {
                box.Stroke = WhiteBrush;
                box.StrokeThickness = 1.5;
            }
            Canvas.SetLeft(box, at.X);
            Canvas.SetTop(box, at.Y);
            MiniCanvas.Children.Add(box);
        }

        // Re-apply the visible-area rectangle (it may have been set before
        // the graph was laid out).
        ApplyViewRect();
    }

    private void ApplyViewRect()
    {
        Rect? rect = _lastViewRect;
        if (_scale <= 0 || rect is null || rect.Value.Width <= 0 || rect.Value.Height <= 0)
        {
            if (_viewRect is not null)
            {
                _viewRect.IsVisible = false;
                _viewRect = null;
            }
            return;
        }

        if (_viewRect is null)
        {
            _viewRect = new Rectangle
            {
                Stroke = GraphLayout.GetAccentBrush(),
                StrokeThickness = 1.5,
                Fill = ViewRectFill,
                IsHitTestVisible = false,
            };
            MiniCanvas.Children.Add(_viewRect);
        }

        // Keep the rectangle above the node boxes.
        if (MiniCanvas.Children[^1] != _viewRect)
        {
            MiniCanvas.Children.Remove(_viewRect);
            MiniCanvas.Children.Add(_viewRect);
        }

        // Map to mini-map coordinates and intersect with the content
        // bounds: the visible area can be larger than the whole graph
        // (then the indicator's border sits at the content edge) or, when
        // panned far away, entirely outside it (then the indicator is
        // hidden).
        double x0 = _offsetX;
        double y0 = _offsetY;
        double x1 = _offsetX + _contentW * _scale;
        double y1 = _offsetY + _contentH * _scale;
        double rx0 = rect.Value.X * _scale + _offsetX;
        double ry0 = rect.Value.Y * _scale + _offsetY;
        double rx1 = rx0 + rect.Value.Width * _scale;
        double ry1 = ry0 + rect.Value.Height * _scale;
        double x = Math.Clamp(rx0, x0, x1);
        double y = Math.Clamp(ry0, y0, y1);
        double w = Math.Min(rx1, x1) - x;
        double h = Math.Min(ry1, y1) - y;
        _viewRect.IsVisible = w > 0 && h > 0;
        if (!_viewRect.IsVisible)
        {
            return;
        }

        _viewRect.Width = Math.Max(2, w);
        _viewRect.Height = Math.Max(2, h);
        Canvas.SetLeft(_viewRect, x);
        Canvas.SetTop(_viewRect, y);
    }

    private Point Map(Point content) =>
        new Point(_offsetX + content.X * _scale, _offsetY + content.Y * _scale);

    /// <summary>
    /// Verification helper (used by the --screenshot mode): a one-line
    /// snapshot of the mini-map's mapping and visible-area rectangle.
    /// </summary>
    public string MiniMapStateForVerification =>
        $"scale={_scale:0.###} offset=({_offsetX:0.##},{_offsetY:0.##}) " +
        $"viewRect={(_lastViewRect is { } r ? $"({r.X:0.##},{r.Y:0.##},{r.Width:0.##},{r.Height:0.##})" : "none")} " +
        $"rectDrawn={(_viewRect is not null && _viewRect.IsVisible ? "yes" : "no")} " +
        $"nodeOffsets=[{string.Join("; ", _nodeOffsets.Select(kv => $"{kv.Key}:({kv.Value.X:0.##},{kv.Value.Y:0.##})"))}]";

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Properties.IsRightButtonPressed)
        {
            return; // only the left button navigates
        }

        e.Pointer.Capture(Root);
        _navigating = true;
        Root.Cursor = new Cursor(StandardCursorType.SizeAll);
        NavigateAt(e.GetPosition(MiniCanvas));
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_navigating)
        {
            return;
        }

        NavigateAt(e.GetPosition(MiniCanvas));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _navigating = false;
        Root.Cursor = null;
        e.Pointer.Capture(null);
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _navigating = false;
        Root.Cursor = null;
    }

    private void NavigateAt(Point at)
    {
        if (_scale <= 0)
        {
            return;
        }

        // Mini-map point -> content point (the inverse of Map).
        Point content = new Point((at.X - _offsetX) / _scale, (at.Y - _offsetY) / _scale);
        Navigate?.Invoke(content);
    }

    /// <summary>
    /// The mini-map card background: the theme's elevated-surface color when
    /// it is defined, falling back to the base canvas color (verified to
    /// exist), then a fixed dark color. Cached in
    /// <see cref="ThemeResources"/> and refreshed on theme change.
    /// </summary>
    private static IBrush GetCardBrush() => ThemeResources.CardBackground;

    /// <summary>
    /// Re-applies the card background after a theme change (the card is set
    /// once at creation, so it would otherwise stay on the old theme's color).
    /// </summary>
    public void RefreshThemeBrushes()
    {
        Root.Background = GetCardBrush();
    }

}
