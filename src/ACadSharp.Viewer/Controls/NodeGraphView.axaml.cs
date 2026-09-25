using ACadSharp.Objects.Evaluations;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// Draws a layered node graph: node boxes (color-coded by kind) in columns
/// by BFS depth, bezier edges with arrowheads and port-name labels.
/// The target node (depth 0) gets a highlight ring. Supports zooming
/// (Scale / ZoomIn / ZoomOut / FitToView / mouse wheel), panning by
/// dragging, a hover tooltip on nodes and edges, and node selection
/// (click: accent ring + <see cref="OnNodeClicked"/> with the details).
/// </summary>
public partial class NodeGraphView : UserControl
{
    private const double BoxWidth = 170;
    private const double BoxHeight = 48;
    private const double ColumnWidth = 240;
    private const double RowHeight = 80;
    private const double Margin = 20;
    private const double MinScale = 0.1;
    private const double MaxScale = 3.0;
    private const double DragThreshold = 4;

    private static readonly IBrush WhiteBrush = new SolidColorBrush(MediaColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush HoverBrush = new SolidColorBrush(MediaColor.Parse("#FFD0D0D0"));

    private double _scale = 1.0;
    private double _naturalWidth;
    private double _naturalHeight;

    // Interaction state: _panStartOffset is null while not panning; a box
    // press first parks in _pendingSelectBox and becomes a pan once the
    // pointer moves past the drag threshold.
    private Vector? _panStartOffset;
    private Point _panStartPos;
    private Border? _pendingSelectBox;
    private Point _pressPos;
    private Border? _hoverBox;
    private Border? _selectedBox;
    private bool _selectedIsTarget;

    /// <summary>
    /// Raised when a node box is clicked, with the node's full dump.
    /// </summary>
    public Action<string>? OnNodeClicked { get; set; }

    /// <summary>
    /// The current zoom factor (1.0 = natural size).
    /// </summary>
    public double Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Clamp(value, MinScale, MaxScale);
            ApplyScale();
        }
    }

    public NodeGraphView()
    {
        InitializeComponent();

        // Pan: press on empty canvas space and drag; the wheel zooms about
        // the cursor (handled here so the scroll viewer does not scroll).
        GraphCanvas.PointerPressed += OnCanvasPointerPressed;
        GraphCanvas.PointerMoved += OnCanvasPointerMoved;
        GraphCanvas.PointerReleased += OnCanvasPointerReleased;
        GraphCanvas.PointerCaptureLost += OnCanvasPointerCaptureLost;
        GraphCanvas.PointerWheelChanged += OnWheelZoom;
    }

    /// <summary>
    /// Zooms in by one step (1.25×).
    /// </summary>
    public void ZoomIn() => Scale = _scale * 1.25;

    /// <summary>
    /// Zooms out by one step (÷1.25).
    /// </summary>
    public void ZoomOut() => Scale = _scale / 1.25;

    /// <summary>
    /// Scales the graph to fit the visible scroll-view area
    /// (never zooms in beyond the natural size).
    /// </summary>
    public void FitToView()
    {
        if (_naturalWidth <= 0 || _naturalHeight <= 0)
        {
            return;
        }

        // Allow ~16px for the scrollbars on each axis.
        double availableWidth = Scroll.Bounds.Width - 16;
        double availableHeight = Scroll.Bounds.Height - 16;
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return; // not measured yet
        }

        double factor = Math.Min(
            1.0,
            Math.Min(availableWidth / _naturalWidth, availableHeight / _naturalHeight));
        Scale = Math.Max(MinScale, factor);
    }

    /// <summary>
    /// Zooms (1.15× per step) so the point under the cursor stays fixed.
    /// </summary>
    public void ZoomAt(Point at, double delta)
    {
        if (_naturalWidth <= 0 || delta == 0)
        {
            return;
        }

        double factor = delta > 0 ? 1.15 : 1.0 / 1.15;
        double newScale = Math.Clamp(_scale * factor, MinScale, MaxScale);
        if (newScale == _scale)
        {
            return;
        }

        Vector oldOffset = Scroll.Offset;
        double ratio = newScale / _scale;
        _scale = newScale;
        ApplyScale();
        Scroll.Offset = new Vector(
            (at.X + oldOffset.X) * ratio - at.X,
            (at.Y + oldOffset.Y) * ratio - at.Y);
    }

    private void OnWheelZoom(object? sender, PointerWheelEventArgs e)
    {
        ZoomAt(e.GetPosition(Scroll), e.Delta.Y);
        e.Handled = true; // do not let the scroll viewer scroll
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled)
        {
            return; // a node box handled the press
        }

        e.Pointer.Capture(GraphCanvas);
        _panStartOffset = Scroll.Offset;
        _panStartPos = e.GetPosition(Scroll);
        GraphCanvas.Cursor = new Cursor(StandardCursorType.Hand);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panStartOffset is not Vector start)
        {
            return;
        }

        Point pos = e.GetPosition(Scroll);
        Scroll.Offset = new Vector(
            start.X + pos.X - _panStartPos.X,
            start.Y + pos.Y - _panStartPos.Y);
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _panStartOffset = null;
        GraphCanvas.Cursor = null;
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _panStartOffset = null;
        _pendingSelectBox = null;
        GraphCanvas.Cursor = null;
    }

    /// <summary>
    /// Selects a node box: accent ring + <see cref="OnNodeClicked"/>.
    /// </summary>
    private void SelectNode(Border border, GraphNodeInfo node, bool isTarget)
    {
        if (_selectedBox != null)
        {
            SetBoxBorder(_selectedBox, _selectedIsTarget, _hoverBox == _selectedBox);
        }

        _selectedBox = border;
        _selectedIsTarget = isTarget;
        SetBoxBorder(border, isTarget, _hoverBox == border);
        OnNodeClicked?.Invoke(BuildTooltip(node));
    }

    /// <summary>
    /// Applies the border for a box's current state: the accent selection
    /// ring wins, then the hover highlight, then the default (a white ring
    /// for the target, none otherwise).
    /// </summary>
    private void SetBoxBorder(Border border, bool isTarget, bool hovered)
    {
        if (_selectedBox == border)
        {
            border.BorderBrush = GetAccentBrush();
            border.BorderThickness = new Thickness(hovered ? 4 : 3);
            return;
        }

        if (hovered)
        {
            border.BorderBrush = isTarget ? WhiteBrush : HoverBrush;
            border.BorderThickness = new Thickness(isTarget ? 4 : 2);
            return;
        }

        border.BorderBrush = isTarget ? WhiteBrush : null;
        border.BorderThickness = new Thickness(isTarget ? 3 : 0);
    }

    /// <summary>
    /// Verification helper (used by the --screenshot mode): the center of
    /// the n-th node box in the given root's coordinate system (null if
    /// the boxes have not been laid out yet).
    /// </summary>
    public Point? GetNodeBoxCenter(Visual root, int n)
    {
        int count = -1;
        foreach (Control child in GraphCanvas.Children)
        {
            if (child is not Border box || box.Bounds.Width <= 0)
            {
                continue;
            }

            count++;
            if (count != n)
            {
                continue;
            }

            return box.TranslatePoint(
                new Point(box.Bounds.Width / 2, box.Bounds.Height / 2),
                root);
        }

        return null;
    }

    private static IBrush GetAccentBrush()
    {
        // Respect the accent the user picked in the settings flyout
        // (null theme would resolve the light dictionary — pass the
        // actual variant explicitly).
        var app = Application.Current;
        if (app is not null)
        {
            if (app.FindResource(app.ActualThemeVariant, "AccentFillColorDefaultBrush") is IBrush brush)
            {
                return brush;
            }
        }

        return new SolidColorBrush(MediaColor.Parse("#0078D4"));
    }

    private void ApplyScale()
    {
        if (_naturalWidth <= 0)
        {
            return;
        }

        // Scale the rendered content and the layout extent together: the
        // canvas's layout size drives the scroll extent, and the
        // RenderTransform scales the drawing within it.
        GraphCanvas.Width = _naturalWidth * _scale;
        GraphCanvas.Height = _naturalHeight * _scale;
        GraphCanvas.RenderTransform = new ScaleTransform(_scale, _scale);
    }

    public void SetGraph(GraphModel.Result model)
    {
        _selectedBox = null;
        _selectedIsTarget = false;
        _hoverBox = null;
        _pendingSelectBox = null;
        _panStartOffset = null;
        GraphCanvas.Children.Clear();

        if (model.Nodes.Count == 0)
        {
            return;
        }

        // Node positions: the target (depth 0) in the rightmost column.
        Dictionary<int, Point> positions = new();
        Dictionary<int, GraphNodeInfo> byIndex = new();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            double x = (model.MaxDepth - node.Depth) * ColumnWidth + Margin;
            double y = node.Row * RowHeight + Margin;
            positions[node.Index] = new Point(x, y);
            byIndex[node.Index] = node;
        }

        // Edges first (below the node boxes).
        foreach (GraphEdgeInfo edge in model.Edges)
        {
            if (!positions.TryGetValue(edge.FromIndex, out Point from)
                || !positions.TryGetValue(edge.ToIndex, out Point to))
            {
                continue;
            }

            GraphNodeInfo? fromNode = byIndex.TryGetValue(edge.FromIndex, out GraphNodeInfo f) ? f : null;
            GraphNodeInfo? toNode = byIndex.TryGetValue(edge.ToIndex, out GraphNodeInfo t) ? t : null;
            AddEdge(from, to, edge, fromNode, toNode);
        }

        // Node boxes.
        foreach (GraphNodeInfo node in model.Nodes)
        {
            AddNodeBox(node, positions[node.Index]);
        }

        _naturalWidth = (model.MaxDepth + 1) * ColumnWidth + Margin * 2;
        _naturalHeight = Math.Max(
            model.Nodes.GroupBy(n => n.Depth).Select(g => g.Count()).Max() * RowHeight + Margin * 2,
            300);
        ApplyScale();
    }

    private void AddNodeBox(GraphNodeInfo node, Point position)
    {
        // The target (depth 0) gets a highlight ring.
        bool isTarget = node.Depth == 0;

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(GetKindColor(node.Kind)),
            Padding = new Thickness(isTarget ? 13 : 10, isTarget ? 7 : 4),
            MinWidth = BoxWidth,
            Height = BoxHeight,
            BorderBrush = isTarget ? new SolidColorBrush(MediaColor.Parse("#FFFFFF")) : null,
            BorderThickness = isTarget ? new Thickness(3) : new Thickness(0),
        };

        var text = new TextBlock
        {
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = $"{node.Expression.GetType().Name}  (#{node.Index})",
        };
        var valueText = new TextBlock
        {
            Foreground = new SolidColorBrush(MediaColor.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontSize = 11,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = ValueFormatter.Format(node.Expression),
        };

        var stack = new StackPanel
        {
            Spacing = 1,
            Children =
            {
                text,
                valueText,
            },
        };

        border.Child = stack;
        Canvas.SetLeft(border, position.X);
        Canvas.SetTop(border, position.Y);

        // Interaction: hover highlight + floating card, click to select,
        // drag to pan. The box captures the pointer so it keeps receiving
        // moves while the box moves away from the cursor during a pan.
        border.PointerPressed += (_, e) =>
        {
            e.Handled = true; // do not start a canvas pan
            _pendingSelectBox = border;
            _pressPos = e.GetPosition(Scroll);
            e.Pointer.Capture(border);
            border.Cursor = new Cursor(StandardCursorType.Hand);
        };
        border.PointerMoved += (_, e) =>
        {
            Point pos = e.GetPosition(Scroll);
            if (_pendingSelectBox == border)
            {
                bool dragged = Math.Abs(pos.X - _pressPos.X) > DragThreshold
                    || Math.Abs(pos.Y - _pressPos.Y) > DragThreshold;
                if (!dragged)
                {
                    return;
                }

                // The press became a drag: drop the pending selection and
                // clear the (possibly stale) hover state, then start panning.
                _pendingSelectBox = null;
                _panStartOffset = Scroll.Offset;
                _panStartPos = pos;
                if (_hoverBox == border)
                {
                    _hoverBox = null;
                    SetBoxBorder(border, isTarget, false);
                }
                HoverTip.IsVisible = false;
            }

            if (_panStartOffset is not Vector start)
            {
                return;
            }

            Scroll.Offset = new Vector(
                start.X + pos.X - _panStartPos.X,
                start.Y + pos.Y - _panStartPos.Y);
        };
        border.PointerReleased += (_, e) =>
        {
            if (_pendingSelectBox == border)
            {
                _pendingSelectBox = null;
                SelectNode(border, node, isTarget);
            }

            _panStartOffset = null;
            border.Cursor = null;
            e.Pointer.Capture(null);

            // Enter/exited were suppressed while panning; re-evaluate the
            // hover state from the pointer's final position.
            Point at = e.GetPosition(border);
            bool inside = at.X >= 0 && at.Y >= 0
                && at.X < border.Bounds.Width && at.Y < border.Bounds.Height;
            _hoverBox = inside ? border : null;
            SetBoxBorder(border, isTarget, inside);
            if (inside)
            {
                ShowNodeTip(node, e.GetPosition(Overlay));
            }
            else
            {
                HoverTip.IsVisible = false;
            }
        };
        border.PointerCaptureLost += (_, _) =>
        {
            _pendingSelectBox = null;
            _panStartOffset = null;
            border.Cursor = null;
        };
        border.PointerEntered += (_, e) =>
        {
            if (_panStartOffset != null)
            {
                return; // the box is moving under the pointer while panning
            }

            _hoverBox = border;
            SetBoxBorder(border, isTarget, true);
            ShowNodeTip(node, e.GetPosition(Overlay));
        };
        border.PointerExited += (_, _) =>
        {
            if (_panStartOffset != null)
            {
                return;
            }

            if (_hoverBox == border)
            {
                _hoverBox = null;
            }

            SetBoxBorder(border, isTarget, false);
            HoverTip.IsVisible = false;
        };

        GraphCanvas.Children.Add(border);
    }

    private void AddEdge(
        Point from,
        Point to,
        GraphEdgeInfo edge,
        GraphNodeInfo? fromNode,
        GraphNodeInfo? toNode)
    {
        // From the right-middle of the source box to the left-middle of the target box.
        Point start = new Point(from.X + BoxWidth, from.Y + BoxHeight / 2);
        Point end = new Point(to.X, to.Y + BoxHeight / 2);

        double dx = Math.Max(Math.Abs(end.X - start.X) * 0.5, 40);
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
        };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = new Point(start.X + dx, start.Y),
            Point2 = new Point(end.X - dx, end.Y),
            Point3 = end,
        });
        geometry.Figures.Add(figure);

        var line = new Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A)),
            StrokeThickness = 1.5,
        };
        if (edge.IsDashed)
        {
            line.StrokeDashArray = new AvaloniaList<double> { 6, 4 };
        }

        // Hover: thicken the edge and show a floating tooltip near the pointer.
        line.PointerEntered += (_, e) =>
        {
            line.StrokeThickness = 3;
            line.Stroke = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
            ShowEdgeTip(edge, fromNode, toNode, e.GetPosition(Overlay));
        };
        line.PointerExited += (_, _) =>
        {
            line.StrokeThickness = 1.5;
            line.Stroke = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A));
            HoverTip.IsVisible = false;
        };
        line.PointerMoved += (_, e) =>
        {
            if (HoverTip.IsVisible)
            {
                Point p = e.GetPosition(Overlay);
                Canvas.SetLeft(HoverTip, p.X + 14);
                Canvas.SetTop(HoverTip, p.Y + 14);
            }
        };

        GraphCanvas.Children.Add(line);

        // Arrowhead at the target end (pointing along the edge direction).
        Vector direction = end - start;
        if (direction.SquaredLength > 0.01)
        {
            direction = direction.Normalize();
            AddArrowhead(end, direction);
        }

        // Label at the midpoint.
        if (edge.Label.Length > 0)
        {
            Point mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2 - 8);
            var label = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush(MediaColor.FromArgb(0xE0, 0x80, 0x80, 0x80)),
                Text = edge.Label,
            };
            // Rough centering: estimate ~6px per character at font size 11.
            Canvas.SetLeft(label, mid.X - edge.Label.Length * 3);
            Canvas.SetTop(label, mid.Y);
            GraphCanvas.Children.Add(label);
        }
    }

    private void ShowNodeTip(GraphNodeInfo node, Point at)
    {
        SetHoverTip(BuildTooltip(node), at);
    }

    private void ShowEdgeTip(GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode, Point at)
    {
        string tip = $"{fromNode?.Kind ?? "?"} #{edge.FromIndex}  →  {toNode?.Kind ?? "?"} #{edge.ToIndex}";
        tip += edge.Label.Length > 0 ? $"\nport: {edge.Label}" : "\n(unlabeled connection)";
        if (edge.IsDashed)
        {
            tip += "\n(lookup connection)";
        }

        SetHoverTip(tip, at);
    }

    private void SetHoverTip(string text, Point at)
    {
        HoverTipText.Text = text;
        HoverTip.IsVisible = true;
        Canvas.SetLeft(HoverTip, at.X + 14);
        Canvas.SetTop(HoverTip, at.Y + 14);
    }

    private void AddArrowhead(Point at, Vector direction)
    {
        double size = 8;
        Vector normal = new Vector(-direction.Y, direction.X);
        Point p1 = at - direction * size;
        Point p2 = p1 + normal * (size / 2);
        Point p3 = p1 - normal * (size / 2);

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = at,
            IsClosed = true,
        };
        figure.Segments.Add(new LineSegment { Point = p2 });
        figure.Segments.Add(new LineSegment { Point = p3 });
        geometry.Figures.Add(figure);

        GraphCanvas.Children.Add(new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A)),
        });
    }

    private static MediaColor GetKindColor(string kind) => kind switch
    {
        "Parameter" => MediaColor.Parse("#3D7EBF"),
        "Grip" => MediaColor.Parse("#3D9E5F"),
        "Action" => MediaColor.Parse("#C77B3D"),
        "Component" => MediaColor.Parse("#7A7A7A"),
        _ => MediaColor.Parse("#8E6FBF"),
    };

    private static string BuildTooltip(GraphNodeInfo node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{node.Expression.GetType().Name}  (node #{node.Index}, {node.Kind})");
        sb.AppendLine($"value: {ValueFormatter.Format(node.Expression)}");
        sb.AppendLine($"id: {node.Expression.Id}");

        if (node.Expression is BlockGrip grip)
        {
            sb.AppendLine($"location: {grip.Location}  displacement: {grip.Displacement}");
        }

        if (node.Expression is BlockParameter parameter)
        {
            sb.AppendLine($"element name: {parameter.ElementName}");
        }

        return sb.ToString();
    }
}
