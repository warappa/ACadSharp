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
    // A named node shows three lines (name / type / value), so its box is
    // taller than the two-line boxes.
    private const double NamedBoxHeight = 64;
    // Column pitch: the horizontal gap between node boxes is ColumnWidth -
    // BoxWidth (150px). The edge labels sit in that gap (centered on the
    // line, 16px clear of the arrowhead), so the pitch must leave room for
    // the longest port-name label (e.g. "Displacement lookup ×2", ~120px).
    private const double ColumnWidth = 320;
    private const double RowHeight = 80;
    private const double Margin = 20;
    private const double MinScale = 0.1;
    private const double MaxScale = 3.0;
    private const double DragThreshold = 4;

    private static readonly IBrush WhiteBrush = new SolidColorBrush(MediaColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush HoverBrush = new SolidColorBrush(MediaColor.Parse("#FFD0D0D0"));
    private static readonly IBrush EdgeLineBrush = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A));
    private static readonly IBrush EdgeLabelBrush = new SolidColorBrush(MediaColor.FromArgb(0xE0, 0x80, 0x80, 0x80));
    private static readonly IBrush EdgeHoverBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
    private static readonly IBrush FeedbackBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
    private static readonly IBrush FeedbackLabelBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));

    private double _scale = 1.0;
    private double _naturalWidth;
    private double _naturalHeight;

    // Content translation in viewport coordinates. Zoom and pan are driven
    // by a scale+translate transform on the canvas (see ApplyScale) rather
    // than the ScrollViewer's offset, so the anchor works at any window
    // size (the canvas is always the viewport size and never scrolls).
    private double _panX;
    private double _panY;

    // Interaction state: _panStartPan is null while not panning; a box
    // press first parks in _pendingSelectBox and becomes a pan once the
    // pointer moves past the drag threshold.
    private Vector? _panStartPan;
    private Point _panStartPos;
    private Border? _pendingSelectBox;
    private Point _pressPos;
    private Border? _hoverBox;
    private Border? _selectedBox;
    private bool _selectedIsTarget;

    // Cross-highlighting: edge Path → its connected port circles, and
    // port circle → its connected edge Path. Populated after all drawing
    // in SetGraph.
    private readonly List<(Path line, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> _pendingEdges = new();
    private readonly Dictionary<(int nodeIdx, int portIdx, bool isInput), Ellipse> _portCircles = new();
    private readonly Dictionary<Path, (Ellipse? src, Ellipse? dst)> _edgeToCircles = new();
    private readonly Dictionary<Ellipse, Path> _circleToEdge = new();

    // Edge labels that still need their final position (the text width is
    // only known after the first layout pass): (mask, label, start.X, end.X).
    private readonly List<(Border mask, TextBlock label, double startX, double endX)> _pendingLabels = new();

    // The first edge line that has a label (for the hover-verification
    // helper, which needs a point on a labeled edge).
    private Path? _firstLabeledEdge;

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

        // Re-apply the canvas size once the scroll viewport has a size
        // (the first layout, and window resizes) so the canvas keeps
        // filling the viewport. Setting the same size again does not
        // invalidate the layout, so this settles after one pass.
        Scroll.LayoutUpdated += (_, _) =>
        {
            if (Scroll.Bounds.Width > 0 && Scroll.Bounds.Height > 0)
            {
                ApplyScale();
            }

            PositionPendingLabels();
        };

        // Backup: the canvas's own layout pass (in case the scroll viewer's
        // LayoutUpdated fires before the labels have been measured).
        GraphCanvas.LayoutUpdated += (_, _) => PositionPendingLabels();
    }

    /// <summary>
    /// Parks each pending edge label at the edge midpoint, shifted left so
    /// its right edge stays at least 16px clear of the arrowhead tip
    /// (the 8px arrowhead plus 8px of visible gap), once the text width is
    /// known. For long labels on short edges that shifts the label left
    /// over the source box's edge — the labels draw above the boxes, so
    /// the overlap stays readable. Labels that are not measured yet are
    /// left for the next layout pass.
    /// </summary>
    private void PositionPendingLabels()
    {
        if (_pendingLabels.Count == 0)
        {
            return;
        }

        for (int i = _pendingLabels.Count - 1; i >= 0; i--)
        {
            (Border mask, TextBlock label, double startX, double endX) = _pendingLabels[i];
            double w = label.DesiredSize.Width;
            if (w <= 0)
            {
                continue; // not measured yet; try on the next pass
            }

            double centeredLeft = (startX + endX) / 2 - w / 2;
            double labelLeft = Math.Min(centeredLeft, endX - 16 - w);
            Canvas.SetLeft(mask, labelLeft - 2); // -2 = mask left padding
            _pendingLabels.RemoveAt(i);
        }
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

        if (Scroll.Bounds.Width <= 0 || Scroll.Bounds.Height <= 0)
        {
            return; // not measured yet
        }

        double factor = Math.Min(
            1.0,
            Math.Min(Scroll.Bounds.Width / _naturalWidth, Scroll.Bounds.Height / _naturalHeight));
        _scale = Math.Max(MinScale, factor);

        // Center the graph in the viewport.
        _panX = (Scroll.Bounds.Width - _naturalWidth * _scale) / 2;
        _panY = (Scroll.Bounds.Height - _naturalHeight * _scale) / 2;
        ApplyScale();
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

        // Keep the point under the cursor fixed: a content point c appears at
        // c*scale + pan, so after scaling by ratio the pan must become
        // pan' = at - (at - pan) * ratio to keep c at the same viewport point.
        double ratio = newScale / _scale;
        _panX = at.X - (at.X - _panX) * ratio;
        _panY = at.Y - (at.Y - _panY) * ratio;
        _scale = newScale;
        ApplyScale();
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
        _panStartPan = new Vector(_panX, _panY);
        _panStartPos = e.GetPosition(Scroll);
        GraphCanvas.Cursor = new Cursor(StandardCursorType.Hand);
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panStartPan is not Vector start)
        {
            return;
        }

        Point pos = e.GetPosition(Scroll);
        _panX = start.X + pos.X - _panStartPos.X;
        _panY = start.Y + pos.Y - _panStartPos.Y;
        UpdateTransform();
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _panStartPan = null;
        GraphCanvas.Cursor = null;
        e.Pointer.Capture(null);
    }

    private void OnCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _panStartPan = null;
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
    /// ring wins, then the hover highlight, then the default (a dim white
    /// ring for the target, invisible otherwise). The thickness is constant
    /// per box (3 for the target, 2 otherwise) so the inner text never
    /// shifts when the hover state changes — only the brush changes
    /// (transparent = invisible but still reserves the border slot).
    /// </summary>
    private void SetBoxBorder(Border border, bool isTarget, bool hovered)
    {
        border.BorderThickness = new Thickness(isTarget ? 3 : 2);

        if (_selectedBox == border)
        {
            border.BorderBrush = GetAccentBrush();
            return;
        }

        if (hovered)
        {
            border.BorderBrush = isTarget ? WhiteBrush : HoverBrush;
            return;
        }

        border.BorderBrush = isTarget
            ? new SolidColorBrush(MediaColor.FromArgb(0x80, 0xFF, 0xFF, 0xFF))
            : Brushes.Transparent;
    }

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode):
    /// the current content pan (the graph is positioned by a scale+translate
    /// transform, not the scroll offset).
    /// </summary>
    public Vector ScrollOffsetForVerification => new Vector(_panX, _panY);

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode):
    /// a one-line snapshot of the zoom/pan state for diagnostics.
    /// </summary>
    public string ScrollStateForVerification =>
        $"scale={_scale:0.###} pan=({_panX:0.##},{_panY:0.##}) " +
        $"canvas={GraphCanvas.Bounds} natural={_naturalWidth:0}x{_naturalHeight:0}";

    /// <summary>
    /// Verification helper (used by the --screenshot zoombug mode): reset
    /// the view to the natural size at the top-left (scale 1, pan 0), as
    /// the user would by fitting the graph back into the viewport.
    /// </summary>
    public void ScrollToOriginForVerification()
    {
        _scale = 1.0;
        _panX = 0;
        _panY = 0;
        ApplyScale();
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

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode): a point
    /// on the first edge that has a label (the midpoint of its bezier, in
    /// the given root's coordinate system), or null if no such edge exists.
    /// </summary>
    public Point? GetLabeledEdgePoint(Visual root)
    {
        if (_firstLabeledEdge is null
            || _firstLabeledEdge.Data is not PathGeometry geometry
            || geometry.Figures.Count == 0)
        {
            return null;
        }

        var figure = geometry.Figures[0];
        if (figure.Segments.Count == 0 || figure.Segments[0] is not BezierSegment bezier)
        {
            return null;
        }

        // The midpoint of the cubic bezier (t = 0.5):
        // (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3,
        // i.e. weights 1/8, 3/8, 3/8, 1/8.
        Point at = new Point(
            0.125 * figure.StartPoint.X + 0.375 * bezier.Point1.X
            + 0.375 * bezier.Point2.X + 0.125 * bezier.Point3.X,
            0.125 * figure.StartPoint.Y + 0.375 * bezier.Point1.Y
            + 0.375 * bezier.Point2.Y + 0.125 * bezier.Point3.Y);
        return _firstLabeledEdge.TranslatePoint(at, root);
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

    /// <summary>
    /// The opaque canvas background (the window background color from the
    /// current theme — verified to match the dialog background in both
    /// themes). Used for the edge-label background mask.
    /// </summary>
    private static IBrush GetCanvasBackgroundBrush()
    {
        var app = Application.Current;
        if (app is not null)
        {
            if (app.FindResource(app.ActualThemeVariant, "SolidBackgroundFillColorBase") is IBrush brush)
            {
                return brush;
            }
        }

        return new SolidColorBrush(MediaColor.Parse("#202020"));
    }

    private void ApplyScale()
    {
        if (_naturalWidth <= 0)
        {
            return;
        }

        // The canvas is always the viewport size (it never scrolls): the
        // graph is positioned inside it by a scale+translate transform, so
        // the empty space around the graph is part of the canvas too and
        // the pointer handlers (wheel zoom, drag pan) work anywhere in the
        // viewport.
        if (Scroll.Bounds.Width > 0 && Scroll.Bounds.Height > 0)
        {
            GraphCanvas.Width = Scroll.Bounds.Width;
            GraphCanvas.Height = Scroll.Bounds.Height;
        }
        UpdateTransform();
    }

    // Scales the graph about the canvas origin and translates it by the
    // current pan, so a content point c appears at c*scale + pan.
    private void UpdateTransform()
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(_scale, _scale));
        group.Children.Add(new TranslateTransform(_panX, _panY));
        GraphCanvas.RenderTransform = group;
    }

    public void SetGraph(GraphModel.Result model)
    {
        _selectedBox = null;
        _selectedIsTarget = false;
        _hoverBox = null;
        _pendingSelectBox = null;
        _panStartPan = null;
        _panX = 0;
        _panY = 0;
        _pendingLabels.Clear();
        _firstLabeledEdge = null;
        _pendingEdges.Clear();
        _portCircles.Clear();
        _edgeToCircles.Clear();
        _circleToEdge.Clear();
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

        // Port positions: distributed evenly along the left/right edge of the box.
        Dictionary<int, List<Point>> inputPortPos = new();
        Dictionary<int, List<Point>> outputPortPos = new();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            Point pos = positions[node.Index];
            double h = BoxHeightFor(node);

            inputPortPos[node.Index] = new List<Point>();
            for (int i = 0; i < node.InputPorts.Count; i++)
            {
                double y = pos.Y + (i + 0.5) * (h / Math.Max(1, node.InputPorts.Count));
                inputPortPos[node.Index].Add(new Point(pos.X, y));
            }

            outputPortPos[node.Index] = new List<Point>();
            for (int i = 0; i < node.OutputPorts.Count; i++)
            {
                double y = pos.Y + (i + 0.5) * (h / Math.Max(1, node.OutputPorts.Count));
                outputPortPos[node.Index].Add(new Point(pos.X + BoxWidth, y));
            }
        }

        // Edges first (below the node boxes).
        foreach (GraphEdgeInfo edge in model.Edges)
        {
            if (!byIndex.TryGetValue(edge.FromIndex, out GraphNodeInfo? fromNode)
                || !byIndex.TryGetValue(edge.ToIndex, out GraphNodeInfo? toNode))
            {
                continue;
            }

            int srcIdx = FindPortIndex(fromNode.OutputPorts, edge.ToIndex);
            int dstIdx = FindPortIndex(toNode.InputPorts, edge.FromIndex);
            Point start = outputPortPos[edge.FromIndex][srcIdx];
            Point end = inputPortPos[edge.ToIndex][dstIdx];
            AddEdge(start, end, edge, fromNode, toNode);
        }

        // Node boxes.
        foreach (GraphNodeInfo node in model.Nodes)
        {
            AddNodeBox(node, positions[node.Index]);
        }

        // Edge labels last: above the node boxes, so a label wider than
        // the gap stays readable where it overlaps the source box.
        foreach (var (mask, _, _, _) in _pendingLabels)
        {
            GraphCanvas.Children.Add(mask);
        }

        // Build the cross-highlighting mappings (edge ↔ port circle).
        foreach (var (line, fromIdx, toIdx, srcPortIdx, dstPortIdx) in _pendingEdges)
        {
            Ellipse? src = _portCircles.TryGetValue((fromIdx, srcPortIdx, false), out var s) ? s : null;
            Ellipse? dst = _portCircles.TryGetValue((toIdx, dstPortIdx, true), out var d) ? d : null;
            _edgeToCircles[line] = (src, dst);
            if (src is not null) _circleToEdge[src] = line;
            if (dst is not null) _circleToEdge[dst] = line;
        }
        _pendingEdges.Clear();
        _portCircles.Clear();

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

        // The element's name (parameters, grips, and actions all carry one):
        // when present it is the primary label and the type drops to the
        // secondary line; without a name the type stays primary.
        string? name = GetName(node.Expression);
        bool hasName = name is not null;

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(GetKindColor(node.Kind)),
            Padding = new Thickness(isTarget ? 13 : 10, isTarget ? 7 : 4),
            MinWidth = BoxWidth,
            Height = hasName ? NamedBoxHeight : BoxHeight,
            // Constant thickness (3 for the target, 2 otherwise) so hover
            // never shifts the text; the brush is set by SetBoxBorder.
            BorderBrush = isTarget ? new SolidColorBrush(MediaColor.FromArgb(0x80, 0xFF, 0xFF, 0xFF)) : Brushes.Transparent,
            BorderThickness = isTarget ? new Thickness(3) : new Thickness(2),
        };

        var text = new TextBlock
        {
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = hasName ? name! : $"{node.Expression.GetType().Name}  (#{node.Index})",
        };
        var lines = new List<TextBlock> { text };

        if (hasName)
        {
            // The type is secondary when the name is primary.
            lines.Add(new TextBlock
            {
                Foreground = new SolidColorBrush(MediaColor.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Text = $"{node.Expression.GetType().Name}  #{node.Index}",
            });
        }

        lines.Add(new TextBlock
        {
            Foreground = new SolidColorBrush(MediaColor.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontSize = 11,
            FontFamily = new FontFamily("Cascadia Code, Consolas, monospace"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = ValueFormatter.Format(node.Expression),
        });

        var stack = new StackPanel { Spacing = 1 };
        foreach (TextBlock line in lines)
        {
            stack.Children.Add(line);
        }

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
                _panStartPan = new Vector(_panX, _panY);
                _panStartPos = pos;
                if (_hoverBox == border)
                {
                    _hoverBox = null;
                    SetBoxBorder(border, isTarget, false);
                }
                HoverTip.IsVisible = false;
            }

            if (_panStartPan is not Vector start)
            {
                return;
            }

            _panX = start.X + pos.X - _panStartPos.X;
            _panY = start.Y + pos.Y - _panStartPos.Y;
            UpdateTransform();
        };
        border.PointerReleased += (_, e) =>
        {
            if (_pendingSelectBox == border)
            {
                _pendingSelectBox = null;
                SelectNode(border, node, isTarget);
            }

            _panStartPan = null;
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
            _panStartPan = null;
            border.Cursor = null;
        };
        border.PointerEntered += (_, e) =>
        {
            if (_panStartPan != null)
            {
                return; // the box is moving under the pointer while panning
            }

            _hoverBox = border;
            SetBoxBorder(border, isTarget, true);
            ShowNodeTip(node, e.GetPosition(Overlay));
        };
        border.PointerExited += (_, _) =>
        {
            if (_panStartPan != null)
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

        // Port circles and labels: input ports on the left edge, output
        // ports on the right edge.
        double h = hasName ? NamedBoxHeight : BoxHeight;
        DrawPorts(node.InputPorts, node.Index, position.X, position.Y, h, isInput: true);
        DrawPorts(node.OutputPorts, node.Index, position.X + BoxWidth, position.Y, h, isInput: false);
    }

    /// <summary>
    /// Draws a row of port circles (small filled circles) along the given
    /// edge of a box, with the port name in small font next to each circle.
    /// </summary>
    private void DrawPorts(List<PortInfo> ports, int nodeIndex, double edgeX, double boxY, double boxH, bool isInput)
    {
        if (ports.Count == 0)
        {
            return;
        }

        const double radius = 4;
        for (int i = 0; i < ports.Count; i++)
        {
            PortInfo port = ports[i];
            double y = boxY + (i + 0.5) * (boxH / ports.Count);

            // Circle (hit-test visible for cross-highlighting).
            var circle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(MediaColor.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1,
            };
            Canvas.SetLeft(circle, edgeX - radius);
            Canvas.SetTop(circle, y - radius);
            GraphCanvas.Children.Add(circle);

            // Store for cross-highlighting.
            _portCircles[(nodeIndex, i, isInput)] = circle;

            // Hover: highlight the circle and the connected edge.
            circle.PointerEntered += (_, e) =>
            {
                circle.Fill = EdgeHoverBrush;
                if (_circleToEdge.TryGetValue(circle, out var edgeLine))
                {
                    edgeLine.StrokeThickness = 3;
                    edgeLine.Stroke = EdgeHoverBrush;
                }
                ShowPortTip(port, isInput, e.GetPosition(Overlay));
            };
            circle.PointerExited += (_, _) =>
            {
                circle.Fill = Brushes.White;
                if (_circleToEdge.TryGetValue(circle, out var edgeLine))
                {
                    edgeLine.StrokeThickness = 1.5;
                    edgeLine.Stroke = EdgeLineBrush;
                }
                HoverTip.IsVisible = false;
            };
            circle.PointerMoved += (_, e) =>
            {
                if (HoverTip.IsVisible)
                {
                    Point p = e.GetPosition(Overlay);
                    Canvas.SetLeft(HoverTip, p.X + 14);
                    Canvas.SetTop(HoverTip, p.Y + 14);
                }
            };

            // Permanent label: to the left of input ports, to the right of
            // output ports. Fully opaque white, 11px, with a background
            // mask so it stays readable over edge lines.
            if (port.Name.Length > 0)
            {
                var label = new TextBlock
                {
                    FontSize = 11,
                    Foreground = Brushes.White,
                    Text = port.Name,
                    IsHitTestVisible = false,
                };
                var mask = new Border
                {
                    Background = GetCanvasBackgroundBrush(),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(2, 0),
                    Child = label,
                    IsHitTestVisible = false,
                };
                if (isInput)
                {
                    Canvas.SetLeft(mask, edgeX - radius - port.Name.Length * 6.5 - 4);
                }
                else
                {
                    Canvas.SetLeft(mask, edgeX + radius + 2);
                }
                Canvas.SetTop(mask, y - 8);
                GraphCanvas.Children.Add(mask);
            }
        }
    }

    private void ShowPortTip(PortInfo port, bool isInput, Point at)
    {
        string side = isInput ? "input" : "output";
        SetHoverTip($"port: {port.Name}\n({side} slot)", at);
    }

    /// <summary>
    /// Finds the index of the port in the given list that connects to the
    /// given peer node index. Returns 0 when no match is found.
    /// </summary>
    private static int FindPortIndex(List<PortInfo> ports, int peerIndex)
    {
        for (int i = 0; i < ports.Count; i++)
        {
            if (ports[i].PeerIndex == peerIndex)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// Highlights or unhighlights the port circles connected to the given
    /// edge line (for cross-highlighting on edge hover).
    /// </summary>
    private void SetPortHighlight(Path line, bool highlight)
    {
        if (!_edgeToCircles.TryGetValue(line, out var circles))
        {
            return;
        }

        IBrush brush = highlight ? EdgeHoverBrush : WhiteBrush;
        if (circles.src is not null) circles.src.Fill = brush;
        if (circles.dst is not null) circles.dst.Fill = brush;
    }

    private void AddEdge(
        Point start,
        Point end,
        GraphEdgeInfo edge,
        GraphNodeInfo? fromNode,
        GraphNodeInfo? toNode)
    {
        if (edge.IsFeedback)
        {
            AddFeedbackEdge(start, end, edge, fromNode, toNode);
            return;
        }

        Vector direction = end - start;
        bool hasDirection = direction.SquaredLength > 0.01;
        if (hasDirection)
        {
            direction = direction.Normalize();
        }

        // Shorten the line by 5px so it doesn't overlap the arrowhead tip.
        Point lineEnd = hasDirection ? end - direction * 5 : end;

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
            Point2 = new Point(lineEnd.X - dx, lineEnd.Y),
            Point3 = lineEnd,
        });
        geometry.Figures.Add(figure);

        var line = new Path
        {
            Data = geometry,
            Stroke = EdgeLineBrush,
            StrokeThickness = 1.5,
        };
        if (edge.IsDashed)
        {
            line.StrokeDashArray = new AvaloniaList<double> { 6, 4 };
        }

        GraphCanvas.Children.Add(line);

        // Record for cross-highlighting (edge ↔ port circle).
        int srcPortIdx = fromNode is not null ? FindPortIndex(fromNode.OutputPorts, edge.ToIndex) : 0;
        int dstPortIdx = toNode is not null ? FindPortIndex(toNode.InputPorts, edge.FromIndex) : 0;
        _pendingEdges.Add((line, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

        // Arrowhead at the target end (pointing along the edge direction).
        Path? arrowhead = null;
        if (hasDirection)
        {
            arrowhead = AddArrowhead(end, direction);
        }

        // Label at the midpoint; the opaque background mask (the canvas
        // background color) keeps the label readable where it overlaps the
        // edge line. The label is added to the canvas AFTER the node boxes
        // (see SetGraph), so a label that is wider than the gap stays
        // readable as a badge over the source box's edge instead of being
        // hidden behind it. The final position is refined after layout,
        // once the text width is known: centered on the edge midpoint,
        // shifted left so the right edge stays at least 16px clear of the
        // arrowhead tip.
        TextBlock? label = null;
        if (edge.Label.Length > 0)
        {
            Point mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2 - 8);
            label = new TextBlock
            {
                FontSize = 11,
                Foreground = EdgeLabelBrush,
                Text = edge.Label,
            };
            var labelMask = new Border
            {
                Background = GetCanvasBackgroundBrush(),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 1),
                Child = label,
                IsHitTestVisible = false, // the edge line keeps its hover
            };
            // Provisional: centered on the midpoint, shifted a bit left; the
            // final position is set by PositionPendingLabels once the text
            // width is known.
            Canvas.SetLeft(labelMask, mid.X - edge.Label.Length * 3);
            Canvas.SetTop(labelMask, mid.Y);

            _pendingLabels.Add((labelMask, label, start.X, end.X));
            // NOTE: the mask is added to the canvas in SetGraph, after the
            // node boxes (so it draws above them).
            if (_firstLabeledEdge is null)
            {
                _firstLabeledEdge = line;
            }
        }

        // Hover: thicken the edge (and highlight the arrowhead, the label,
        // and the connected port circles) and show a floating tooltip.
        line.PointerEntered += (_, e) =>
        {
            line.StrokeThickness = 3;
            line.Stroke = EdgeHoverBrush;
            if (arrowhead is not null)
            {
                arrowhead.Fill = EdgeHoverBrush;
            }
            if (label is not null)
            {
                label.Foreground = EdgeHoverBrush;
                label.FontWeight = FontWeight.SemiBold;
            }
            SetPortHighlight(line, true);
            ShowEdgeTip(edge, fromNode, toNode, e.GetPosition(Overlay));
        };
        line.PointerExited += (_, _) =>
        {
            line.StrokeThickness = 1.5;
            line.Stroke = EdgeLineBrush;
            if (arrowhead is not null)
            {
                arrowhead.Fill = EdgeLineBrush;
            }
            if (label is not null)
            {
                label.Foreground = EdgeLabelBrush;
                label.FontWeight = FontWeight.Normal;
            }
            SetPortHighlight(line, false);
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
    }

    /// <summary>
    /// Draws a feedback edge: a dashed arc arcing above the boxes, from the
    /// top-center of the source to the top-center of the target, in a
    /// distinct orange color. The arrowhead points along the curve tangent
    /// at the target end.
    /// </summary>
    private void AddFeedbackEdge(
        Point start,
        Point end,
        GraphEdgeInfo edge,
        GraphNodeInfo? fromNode,
        GraphNodeInfo? toNode)
    {

        double arcHeight = Math.Max(50, Math.Abs(end.X - start.X) * 0.25);
        Point c1 = new Point(start.X, start.Y - arcHeight);
        Point c2 = new Point(end.X, end.Y - arcHeight);

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
        };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = c1,
            Point2 = c2,
            Point3 = end,
        });
        geometry.Figures.Add(figure);

        var line = new Path
        {
            Data = geometry,
            Stroke = FeedbackBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new AvaloniaList<double> { 6, 4 },
        };
        GraphCanvas.Children.Add(line);

        // Record for cross-highlighting (edge ↔ port circle).
        int srcPortIdx = fromNode is not null ? FindPortIndex(fromNode.OutputPorts, edge.ToIndex) : 0;
        int dstPortIdx = toNode is not null ? FindPortIndex(toNode.InputPorts, edge.FromIndex) : 0;
        _pendingEdges.Add((line, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

        // Arrowhead at the target end, pointing along the curve tangent
        // (the direction from the last control point toward the endpoint).
        Vector dir = end - c2;
        if (dir.SquaredLength > 0.01)
        {
            dir = dir.Normalize();
            AddArrowhead(end, dir, FeedbackBrush);
        }

        // Label at the arc apex (bezier midpoint, t = 0.5).
        if (edge.Label.Length > 0)
        {
            double apexX = 0.125 * start.X + 0.375 * c1.X + 0.375 * c2.X + 0.125 * end.X;
            double apexY = 0.125 * start.Y + 0.375 * c1.Y + 0.375 * c2.Y + 0.125 * end.Y;

            var label = new TextBlock
            {
                FontSize = 11,
                Foreground = FeedbackLabelBrush,
                Text = edge.Label,
            };
            var labelMask = new Border
            {
                Background = GetCanvasBackgroundBrush(),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 1),
                Child = label,
                IsHitTestVisible = false,
            };
            // Position the label at the apex, centered horizontally.
            double labelWidth = edge.Label.Length * 7; // rough estimate
            Canvas.SetLeft(labelMask, apexX - labelWidth / 2);
            Canvas.SetTop(labelMask, apexY - 12);
            GraphCanvas.Children.Add(labelMask);
        }

        // Hover: thicken the arc, highlight connected port circles, and show a tooltip.
        line.PointerEntered += (_, e) =>
        {
            line.StrokeThickness = 3;
            SetPortHighlight(line, true);
            ShowEdgeTip(edge, fromNode, toNode, e.GetPosition(Overlay));
        };
        line.PointerExited += (_, _) =>
        {
            line.StrokeThickness = 1.5;
            SetPortHighlight(line, false);
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
    }

    private void ShowNodeTip(GraphNodeInfo node, Point at)
    {
        SetHoverTip(BuildTooltip(node), at);
    }

    private void ShowEdgeTip(GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode, Point at)
    {
        // The node's name (when it has one) leads the label, then the kind
        // and index.
        string tip = $"{NodeLabel(fromNode)}  →  {NodeLabel(toNode)}";
        tip += edge.Label.Length > 0 ? $"\nport: {edge.Label}" : "\n(unlabeled connection)";
        if (edge.IsDashed)
        {
            tip += "\n(lookup connection)";
        }

        SetHoverTip(tip, at);
    }

    private static string NodeLabel(GraphNodeInfo? node)
    {
        if (node is null)
        {
            return "?";
        }

        string? name = GetName(node.Expression);
        return name is null
            ? $"{node.Kind} #{node.Index}"
            : $"{name} ({node.Kind}) #{node.Index}";
    }

    private void SetHoverTip(string text, Point at)
    {
        HoverTipText.Text = text;
        HoverTip.IsVisible = true;
        Canvas.SetLeft(HoverTip, at.X + 14);
        Canvas.SetTop(HoverTip, at.Y + 14);
    }

    private Path AddArrowhead(Point at, Vector direction) => AddArrowhead(at, direction, EdgeLineBrush);

    private Path AddArrowhead(Point at, Vector direction, IBrush brush)
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

        var arrowhead = new Path
        {
            Data = geometry,
            Fill = brush,
        };
        GraphCanvas.Children.Add(arrowhead);
        return arrowhead;
    }

    /// <summary>
    /// The display name of a node's element: the block element name
    /// (parameters, grips, and actions all carry one); null when the element
    /// has no name (e.g. grip location components).
    /// </summary>
    private static string? GetName(EvaluationExpression expression)
    {
        string? name = (expression as BlockElement)?.ElementName;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// The height of a node's box: named nodes show three lines (name /
    /// type / value) and get the taller box; unnamed nodes stay at the
    /// default two-line height.
    /// </summary>
    private static double BoxHeightFor(GraphNodeInfo? node) =>
        node is null || GetName(node.Expression) is null ? BoxHeight : NamedBoxHeight;

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
        string? name = GetName(node.Expression);
        if (name is not null)
        {
            sb.AppendLine($"name: {name}");
        }
        sb.AppendLine($"value: {ValueFormatter.Format(node.Expression)}");
        sb.AppendLine($"id: {node.Expression.Id}");

        if (node.Expression is BlockGrip grip)
        {
            sb.AppendLine($"location: {grip.Location}  displacement: {grip.Displacement}");
        }

        return sb.ToString();
    }
}
