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
    // Layout metrics (box / column / row sizes and the content margin) and the
    // kind-color / box-height / accent rules live in the shared GraphLayout
    // (also used by MiniMapView) so the two views stay in sync.
    private const double DragThreshold = 4;

    private static readonly IBrush WhiteBrush = new SolidColorBrush(MediaColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush HoverBrush = new SolidColorBrush(MediaColor.Parse("#FFD0D0D0"));
    private static readonly IBrush EdgeLineBrush = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A));
    private static readonly IBrush EdgeLabelBrush = new SolidColorBrush(MediaColor.FromArgb(0xE0, 0x80, 0x80, 0x80));
    private static readonly IBrush EdgeHoverBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
    private static readonly IBrush FeedbackBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
    private static readonly IBrush FeedbackLabelBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));

    // The zoom/pan transform (scale + translate on the canvas) is encapsulated
    // in a GraphTransform so the control stays focused on the graph content and
    // the hover/select interaction.
    private GraphTransform _transform = null!;

    // Interaction state: a box press first parks in _pendingSelectBox and
    // becomes a pan once the pointer moves past the drag threshold.
    private Border? _pendingSelectBox;
    private Point _pressPos;
    private Border? _hoverBox;
    private Border? _selectedBox;
    private bool _selectedIsTarget;

    // Node dragging: per-node offset (viewport-independent, in content
    // coordinates). Applied on top of the computed layout position.
    private readonly Dictionary<int, Vector> _nodeOffsets = new();
    // Container per node: holds the box + port circles + port labels.
    // Moving the container moves the whole node group.
    private readonly Dictionary<int, Canvas> _nodeContainers = new();
    // Base (layout) positions per node, without the drag offset.
    private readonly Dictionary<int, Point> _basePositions = new();
    // Box heights per node (for port Y computation).
    private readonly Dictionary<int, double> _boxHeights = new();
    // Persistent edge records for real-time geometry updates during drag.
    // The arrowhead and label (if any) are stored alongside the line so
    // all three update together.
    private readonly List<(Path line, Path? arrowhead, Border? labelMask, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> _edgeRecords = new();
    private Border? _dragBox;
    private int _dragNodeIndex;
    private Point _dragStartPos;
    private Point _dragLastPos;
    private GraphModel.Result? _lastModel;

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

    // Port labels deferred to a separate pass (drawn after all boxes so
    // they are not covered by adjacent-column boxes).
    private readonly List<Border> _pendingPortLabels = new();

    // The first edge line that has a label (for the hover-verification
    // helper, which needs a point on a labeled edge).
    private Path? _firstLabeledEdge;

    /// <summary>
    /// Raised when a node box is clicked, with the node's full dump.
    /// </summary>
    public Action<string>? OnNodeClicked { get; set; }

    /// <summary>
    /// Raised whenever the view's transform (scale or pan) changes, with
    /// the currently visible rectangle in content coordinates (the graph's
    /// natural layout system). The mini-map uses this to draw its
    /// visible-area indicator.
    /// </summary>
    public Action<Rect>? ViewChanged { get; set; }

    /// <summary>
    /// Raised when a node is dragged (its user offset in content
    /// coordinates), so the mini-map can keep the node's overview
    /// position in sync.
    /// </summary>
    public Action<int, Vector>? NodeMoved { get; set; }

    /// <summary>
    /// The currently visible rectangle in content coordinates: the viewport
    /// mapped back through the scale+translate transform (a content point c
    /// appears at c*scale + pan, so the visible content is
    /// ((0-pan)/scale, (0-pan)/scale, viewport/scale)). Empty before the
    /// first layout.
    /// </summary>
    public Rect GetVisibleContentRect() => _transform.GetVisibleContentRect();

    /// <summary>
    /// Centers the view on the given content point (the mini-map's
    /// navigation target).
    /// </summary>
    public void CenterOnContentPoint(Point p) => _transform.CenterOnContentPoint(p);

    /// <summary>
    /// The current zoom factor (1.0 = natural size).
    /// </summary>
    public double Scale
    {
        get => _transform.Scale;
        set => _transform.Scale = value;
    }

    public NodeGraphView()
	{
		InitializeComponent();

		_transform = new GraphTransform(Scroll, GraphCanvas);
		_transform.Changed = rect => ViewChanged?.Invoke(rect);

		InitializeComponentState();
	}
    
    [AvaloniaHotReload]
	private void InitializeComponentState()
	{
		GraphCanvas.RenderTransformOrigin =
                new RelativePoint(0, 0, RelativeUnit.Relative);

		// Pan: press on empty canvas space and drag; the wheel zooms about
		// the cursor (handled here so the scroll viewer does not scroll).
		GraphCanvas.PointerPressed += OnCanvasPointerPressed;
		GraphCanvas.PointerMoved += OnCanvasPointerMoved;
		GraphCanvas.PointerReleased += OnCanvasPointerReleased;
		GraphCanvas.PointerCaptureLost += OnCanvasPointerCaptureLost;
		// Attach to the ScrollViewer (full viewport), not the GraphCanvas
		// (whose layout bounds are only as large as the content). The
		// RenderTransform scales the canvas's rendering, but hit-testing
		// uses layout bounds — so the wheel would not fire over the empty
		// area around the content if attached to the canvas.
		Scroll.PointerWheelChanged += OnWheelZoom;

		// Re-apply the canvas size once the scroll viewport has a size
		// (the first layout, and window resizes) so the canvas keeps
		// filling the viewport. Setting the same size again does not
		// invalidate the layout, so this settles after one pass.
		Scroll.LayoutUpdated += (_, _) =>
		{
			if (Scroll.Bounds.Width > 0 && Scroll.Bounds.Height > 0)
			{
				_transform.Apply();
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
    /// Zooms in by one step (1.15×), anchored at the viewport center.
    /// </summary>
    public void ZoomIn() => _transform.ZoomIn();

    /// <summary>
    /// Zooms out by one step (÷1.15), anchored at the viewport center.
    /// </summary>
    public void ZoomOut() => _transform.ZoomOut();

    /// <summary>
    /// Scales the graph to fit the visible scroll-view area
    /// (never zooms in beyond the natural size).
    /// </summary>
    public void FitToView() => _transform.FitToView();

    /// <summary>
    /// Zooms (1.15× per step) so the point under the cursor stays fixed.
    /// </summary>
    public void ZoomAt(Point at, double delta) => _transform.ZoomAt(at, delta);

    private void OnWheelZoom(object? sender, PointerWheelEventArgs e) => _transform.OnWheelZoom(e);

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e) => _transform.OnPointerPressed(e);

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e) => _transform.OnPointerMoved(e);

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e) => _transform.OnPointerReleased(e);

    private void OnCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _transform.OnPointerCaptureLost();
        _pendingSelectBox = null;
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
        OnNodeClicked?.Invoke(GraphDrawing.BuildTooltip(node));
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
            border.BorderBrush = GraphLayout.GetAccentBrush();
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
    /// Re-applies the theme-dependent brushes after a theme change: the
    /// selected node's accent ring (the edge-label masks are recreated on the
    /// next draw and pick up the refreshed <see cref="ThemeResources"/> cache
    /// then).
    /// </summary>
    public void RefreshThemeBrushes()
    {
        if (_selectedBox is not null)
        {
            SetBoxBorder(_selectedBox, _selectedIsTarget, _hoverBox == _selectedBox);
        }
    }

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode):
    /// the current content pan (the graph is positioned by a scale+translate
    /// transform, not the scroll offset).
    /// </summary>
    public Vector ScrollOffsetForVerification => _transform.ScrollOffset;

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode):
    /// a one-line snapshot of the zoom/pan state for diagnostics.
    /// </summary>
    public string ScrollStateForVerification
    {
        get
        {
            Vector pan = _transform.ScrollOffset;
            Vector natural = _transform.NaturalSize;
            return $"scale={_transform.Scale:0.###} pan=({pan.X:0.##},{pan.Y:0.##}) " +
                $"canvas={GraphCanvas.Bounds} natural={natural.X:0}x{natural.Y:0}";
        }
    }

    /// <summary>
    /// Verification helper (used by the --screenshot zoombug mode): reset
    /// the view to the natural size at the top-left (scale 1, pan 0), as
    /// the user would by fitting the graph back into the viewport.
    /// </summary>
    public void ScrollToOriginForVerification() => _transform.ScrollToOrigin();

    /// <summary>
    /// Verification helper (used by the --screenshot mode): the center of
    /// the n-th node box in the given root's coordinate system (null if
    /// the boxes have not been laid out yet). The boxes are the first
    /// Border child of the per-node container Canvases (the canvas's
    /// direct Border children are the edge-label masks, not node boxes).
    /// </summary>
    public Point? GetNodeBoxCenter(Visual root, int n)
    {
        int count = -1;
        foreach (Control child in GraphCanvas.Children)
        {
            if (child is not Canvas container)
            {
                continue;
            }

            // The node box is the container's first Border child (added
            // before the port circles and labels).
            Border? box = null;
            foreach (Control c in container.Children)
            {
                if (c is Border b)
                {
                    box = b;
                    break;
                }
            }
            if (box is null || box.Bounds.Width <= 0)
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
            || geometry.Figures is null || geometry.Figures.Count == 0)
        {
            return null;
        }

        var figure = geometry.Figures[0];
        if (figure.Segments is null || figure.Segments.Count == 0 || figure.Segments[0] is not BezierSegment bezier)
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

    /// <summary>
    /// The opaque canvas background (the window background color from the
    /// current theme — verified to match the dialog background in both
    /// themes). Used for the edge-label background mask. Cached in
    /// <see cref="ThemeResources"/> and refreshed on theme change.
    /// </summary>
    private static IBrush GetCanvasBackgroundBrush() => ThemeResources.CanvasBackground;

    public void SetGraph(GraphModel.Result model, bool resetPan = true)
    {
        _lastModel = model;
        _selectedBox = null;
        _selectedIsTarget = false;
        _hoverBox = null;
        _pendingSelectBox = null;
        _transform.EndPan();
        if (resetPan)
        {
            _transform.ResetPan();
        }
        _pendingLabels.Clear();
        _firstLabeledEdge = null;
        _pendingEdges.Clear();
        _portCircles.Clear();
        _edgeToCircles.Clear();
        _circleToEdge.Clear();
        _nodeContainers.Clear();
        _edgeRecords.Clear();
        GraphCanvas.Children.Clear();

        if (model.Nodes.Count == 0)
        {
            return;
        }

        // Node positions: the target (depth 0) in the rightmost column.
        // Base layout position only — the user-drag offset is applied
        // separately in AddNodeBox so the closure's `position` stays
        // the unmodified layout anchor.
        Dictionary<int, Point> positions = new();
        Dictionary<int, GraphNodeInfo> byIndex = new();
        _basePositions.Clear();
        _boxHeights.Clear();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            double x = (model.MaxDepth - node.Depth) * GraphLayout.ColumnWidth + GraphLayout.LayoutMargin;
            double y = node.Row * GraphLayout.RowHeight + GraphLayout.LayoutMargin;
            positions[node.Index] = new Point(x, y);
            byIndex[node.Index] = node;
            _basePositions[node.Index] = new Point(x, y);
            _boxHeights[node.Index] = GraphLayout.BoxHeightFor(node);
        }

        // Port positions: distributed evenly along the left/right edge of the box.
        // Apply the user-drag offset so ports follow the moved node.
        Dictionary<int, List<Point>> inputPortPos = new();
        Dictionary<int, List<Point>> outputPortPos = new();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            Vector off = _nodeOffsets.GetValueOrDefault(node.Index);
            Point pos = new Point(positions[node.Index].X + off.X, positions[node.Index].Y + off.Y);
            double h = GraphLayout.BoxHeightFor(node);

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
                outputPortPos[node.Index].Add(new Point(pos.X + GraphLayout.BoxWidth, y));
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

        double naturalWidth = (model.MaxDepth + 1) * GraphLayout.ColumnWidth + GraphLayout.LayoutMargin * 2;
        double naturalHeight = Math.Max(
            model.Nodes.GroupBy(n => n.Depth).Select(g => g.Count()).Max() * GraphLayout.RowHeight + GraphLayout.LayoutMargin * 2,
            300);
        _transform.SetNaturalSize(naturalWidth, naturalHeight);
        _transform.Apply();
    }

    private void AddNodeBox(GraphNodeInfo node, Point position)
    {
        // The target (depth 0) gets a highlight ring.
        bool isTarget = node.Depth == 0;

        // The element's name (parameters, grips, and actions all carry one):
        // when present it is the primary label and the type drops to the
        // secondary line; without a name the type stays primary.
        string? name = GraphDrawing.GetName(node.Expression);
        bool hasName = name is not null;

        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(GraphLayout.GetKindColor(node.Kind)),
            Padding = new Thickness(isTarget ? 13 : 10, isTarget ? 7 : 4),
            MinWidth = GraphLayout.BoxWidth,
            Height = hasName ? GraphLayout.NamedBoxHeight : GraphLayout.BoxHeight,
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

        // Container: holds the box (at 0,0) + port circles + port labels.
        // Positioned at the node's layout position + drag offset.
        var container = new Canvas();
        Canvas.SetLeft(border, 0);
        Canvas.SetTop(border, 0);
        container.Children.Add(border);
        _nodeContainers[node.Index] = container;

        Vector offset = _nodeOffsets.GetValueOrDefault(node.Index);
        Canvas.SetLeft(container, position.X + offset.X);
        Canvas.SetTop(container, position.Y + offset.Y);
        GraphCanvas.Children.Add(container);

        // Interaction: left-button drag moves the node; left click selects;
        // right-button propagates to the canvas for panning.
        border.PointerPressed += (_, e) =>
        {
            if (e.Properties.IsRightButtonPressed)
            {
                return; // let the canvas handle right-button pan
            }

            e.Handled = true;
            _pendingSelectBox = border;
            _pressPos = e.GetPosition(Scroll);
            _dragBox = border;
            _dragNodeIndex = node.Index;
            _dragStartPos = e.GetPosition(Scroll);
            _dragLastPos = e.GetPosition(Scroll);
            e.Pointer.Capture(border);
            border.Cursor = new Cursor(StandardCursorType.SizeAll);
        };
        border.PointerMoved += (_, e) =>
        {
            if (_dragBox != border)
            {
                return;
            }

            Point pos = e.GetPosition(Scroll);
            bool dragged = Math.Abs(pos.X - _dragStartPos.X) > DragThreshold
                || Math.Abs(pos.Y - _dragStartPos.Y) > DragThreshold;
            if (!dragged)
            {
                _dragLastPos = pos;
                return;
            }

            // Incremental delta: from the last move position, converted
            // to content space (divide by scale). Accumulates smoothly
            // across moves and across drags.
            double dx = (pos.X - _dragLastPos.X) / _transform.Scale;
            double dy = (pos.Y - _dragLastPos.Y) / _transform.Scale;
            _dragLastPos = pos;

            Vector offset = _nodeOffsets.GetValueOrDefault(node.Index);
            _nodeOffsets[node.Index] = new Vector(offset.X + dx, offset.Y + dy);
            NodeMoved?.Invoke(node.Index, _nodeOffsets[node.Index]);

            // Move the container (box + ports + labels move together).
            if (_nodeContainers.TryGetValue(node.Index, out var container))
            {
                Canvas.SetLeft(container, position.X + _nodeOffsets[node.Index].X);
                Canvas.SetTop(container, position.Y + _nodeOffsets[node.Index].Y);
            }

            // Update connected edges in real-time.
            UpdateConnectedEdges(node.Index);
        };
        border.PointerReleased += (_, e) =>
        {
            if (_dragBox == border)
            {
                _dragBox = null;

                if (_pendingSelectBox == border)
                {
                    // Check if it was a drag or a click.
                    Point pos = e.GetPosition(Scroll);
                    bool wasDrag = Math.Abs(pos.X - _dragStartPos.X) > DragThreshold
                        || Math.Abs(pos.Y - _dragStartPos.Y) > DragThreshold;

                    if (wasDrag)
                    {
                        _pendingSelectBox = null;
                        // Redraw edges to follow the moved node.
                        // Skip pan reset so the view doesn't jump.
                        if (_lastModel is not null)
                        {
                            SetGraph(_lastModel, resetPan: false);
                        }
                    }
                    else
                    {
                        _pendingSelectBox = null;
                        SelectNode(border, node, isTarget);
                    }
                }
            }

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
            _transform.EndPan();
            border.Cursor = null;
        };
        border.PointerEntered += (_, e) =>
        {
            if (_transform.IsPanning)
            {
                return; // the box is moving under the pointer while panning
            }

            _hoverBox = border;
            SetBoxBorder(border, isTarget, true);
            ShowNodeTip(node, e.GetPosition(Overlay));
        };
        border.PointerExited += (_, _) =>
        {
            if (_transform.IsPanning)
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

        // Port circles and labels: input ports on the left edge, output
        // ports on the right edge. Positions are relative to the container's
        // origin (the box's top-left corner).
        double h = hasName ? GraphLayout.NamedBoxHeight : GraphLayout.BoxHeight;
        DrawPorts(node.InputPorts, node.Index, 0, 0, h, isInput: true, container);
        DrawPorts(node.OutputPorts, node.Index, GraphLayout.BoxWidth, 0, h, isInput: false, container);
    }

    /// <summary>
    /// Draws a row of port circles (small filled circles) along the given
    /// edge of a box, with the port name in small font next to each circle.
    /// </summary>
    private void DrawPorts(List<PortInfo> ports, int nodeIndex, double edgeX, double boxY, double boxH, bool isInput, Canvas container)
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
            // Positions are relative to the container's origin.
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
            container.Children.Add(circle);

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
            // mask. Added to the node container so it moves with the node.
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
                container.Children.Add(mask);
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
        figure.Segments!.Add(new BezierSegment
        {
            Point1 = new Point(start.X + dx, start.Y),
            Point2 = new Point(lineEnd.X - dx, lineEnd.Y),
            Point3 = lineEnd,
        });
        geometry.Figures!.Add(figure);

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
        Border? labelMask = null;
        if (edge.Label.Length > 0)
        {
            Point mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2 - 8);
            label = new TextBlock
            {
                FontSize = 11,
                Foreground = EdgeLabelBrush,
                Text = edge.Label,
            };
            labelMask = new Border
            {
                Background = GetCanvasBackgroundBrush(),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 1),
                Child = label,
            };
            // Hovering the label highlights the edge (same as hovering the line).
            labelMask.PointerEntered += (_, e) =>
            {
                line.StrokeThickness = 3;
                line.Stroke = EdgeHoverBrush;
                if (arrowhead is not null) arrowhead.Fill = EdgeHoverBrush;
                label!.Foreground = EdgeHoverBrush;
                label.FontWeight = FontWeight.SemiBold;
                SetPortHighlight(line, true);
                ShowEdgeTip(edge, fromNode, toNode, e.GetPosition(Overlay));
            };
            labelMask.PointerExited += (_, _) =>
            {
                line.StrokeThickness = 1.5;
                line.Stroke = EdgeLineBrush;
                if (arrowhead is not null) arrowhead.Fill = EdgeLineBrush;
                label!.Foreground = EdgeLabelBrush;
                label.FontWeight = FontWeight.Normal;
                SetPortHighlight(line, false);
                HoverTip.IsVisible = false;
            };
            labelMask.PointerMoved += (_, e) =>
            {
                if (HoverTip.IsVisible)
                {
                    Point p = e.GetPosition(Overlay);
                    Canvas.SetLeft(HoverTip, p.X + 14);
                    Canvas.SetTop(HoverTip, p.Y + 14);
                }
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
        _edgeRecords.Add((line, arrowhead, labelMask, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

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
        figure.Segments!.Add(new BezierSegment
        {
            Point1 = c1,
            Point2 = c2,
            Point3 = end,
        });
        geometry.Figures!.Add(figure);

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
        Path? fbArrowhead = null;
        Vector dir = end - c2;
        if (dir.SquaredLength > 0.01)
        {
            dir = dir.Normalize();
            fbArrowhead = AddArrowhead(end, dir, FeedbackBrush);
        }

        // Label at the arc apex (bezier midpoint, t = 0.5).
        Border? fbLabelMask = null;
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
            fbLabelMask = new Border
            {
                Background = GetCanvasBackgroundBrush(),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 1),
                Child = label,
                IsHitTestVisible = false,
            };
            // Position the label at the apex, centered horizontally.
            double labelWidth = edge.Label.Length * 7; // rough estimate
            Canvas.SetLeft(fbLabelMask, apexX - labelWidth / 2);
            Canvas.SetTop(fbLabelMask, apexY - 12);
            GraphCanvas.Children.Add(fbLabelMask);
        }
        _edgeRecords.Add((line, fbArrowhead, fbLabelMask, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

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
        SetHoverTip(GraphDrawing.BuildTooltip(node), at);
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

        string? name = GraphDrawing.GetName(node.Expression);
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
        var arrowhead = new Path
        {
            Data = GraphDrawing.BuildArrowheadGeometry(at, direction),
            Fill = brush,
        };
        GraphCanvas.Children.Add(arrowhead);
        return arrowhead;
    }

    /// <summary>
    /// The height of a node's box: named nodes show three lines (name /
    /// type / value) and get the taller box; unnamed nodes stay at the
    /// <summary>
    /// Recomputes the geometry of all edges connected to the given node,
    /// using the node's current (offset) position. Called during a drag
    /// so the arrows follow the moved node in real-time.
    /// </summary>
    private void UpdateConnectedEdges(int nodeIndex)
    {
        foreach (var (line, arrowhead, labelMask, fromIdx, toIdx, srcPortIdx, dstPortIdx) in _edgeRecords)
        {
            if (fromIdx != nodeIndex && toIdx != nodeIndex)
            {
                continue;
            }

            // Compute the current port positions (base + offset).
            if (!_basePositions.TryGetValue(fromIdx, out Point fromBase)
                || !_basePositions.TryGetValue(toIdx, out Point toBase)
                || !_boxHeights.TryGetValue(fromIdx, out double fromH)
                || !_boxHeights.TryGetValue(toIdx, out double toH))
            {
                continue;
            }

            Vector fromOff = _nodeOffsets.GetValueOrDefault(fromIdx);
            Vector toOff = _nodeOffsets.GetValueOrDefault(toIdx);

            // Output port position (right edge of source).
            double outY = fromBase.Y + fromOff.Y + (srcPortIdx + 0.5) * (fromH / Math.Max(1, GetPortCount(fromIdx, isInput: false)));
            Point start = new Point(fromBase.X + fromOff.X + GraphLayout.BoxWidth, outY);

            // Input port position (left edge of target).
            double inY = toBase.Y + toOff.Y + (dstPortIdx + 0.5) * (toH / Math.Max(1, GetPortCount(toIdx, isInput: true)));
            Point end = new Point(toBase.X + toOff.X, inY);

            // Rebuild the edge geometry.
            line.Data = GraphDrawing.BuildEdgeGeometry(start, end);

            // Update the arrowhead (rebuild the triangle at the new end).
            if (arrowhead is not null)
            {
                Vector direction = end - start;
                if (direction.SquaredLength > 0.01)
                {
                    direction = direction.Normalize();
                    arrowhead.Data = GraphDrawing.BuildArrowheadGeometry(end, direction);
                }
            }

            // Update the label position (centered on the new midpoint).
            if (labelMask is not null)
            {
                Point mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2 - 8);
                double labelWidth = (labelMask.Child as TextBlock)?.Text?.Length * 3 ?? 0;
                Canvas.SetLeft(labelMask, mid.X - labelWidth);
                Canvas.SetTop(labelMask, mid.Y);
            }
        }
    }

    private int GetPortCount(int nodeIndex, bool isInput)
    {
        if (_lastModel is null) return 1;
        var node = _lastModel.Nodes.FirstOrDefault(n => n.Index == nodeIndex);
        return isInput ? Math.Max(1, node?.InputPorts.Count ?? 1) : Math.Max(1, node?.OutputPorts.Count ?? 1);
    }

}
