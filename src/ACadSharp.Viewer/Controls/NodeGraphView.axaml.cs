using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using System;
using System.Collections.Generic;

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
    // (also used by MiniMapView) so the two views stay in sync. The element
    // brushes live with the construction, in GraphScene.

    // The zoom/pan transform (scale + translate on the canvas) is encapsulated
    // in a GraphTransform so the control stays focused on the graph content and
    // the hover/select interaction.
    private GraphTransform _transform = null!;

    // The hover tooltip (a floating label that follows the pointer and
    // describes the hovered node / port / edge).
    private GraphTooltip _tooltip = null!;

    // Read-only verification helpers (the zoom/pan state, the node box
    // centers, the labeled-edge point, and the scene fingerprint) for the
    // --screenshot harness.
    private GraphDiagnostics _diagnostics = null!;

    // The interaction (node selection, hover highlighting, and node dragging)
    // is encapsulated in a GraphInteraction; the construction owns the
    // element tree and the element state and wires each element's pointer
    // events to the interaction's callbacks.
    private GraphInteraction _interaction = null!;

    // The scene: owns the element tree, the element state, and the
    // construction (node boxes, ports, edges, arrowheads). The element state
    // is shared with the interaction (which reads it for the hover/drag
    // behavior).
    private GraphScene _scene = null!;

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
		_tooltip = new GraphTooltip(HoverTip, HoverTipText);

		// The scene is built before the interaction so the shared element
		// state (the dictionaries) can be handed to both; the interaction is
		// wired back into the scene afterwards (the construction runs only
		// after construction, by which point the reference is set).
		_scene = new GraphScene(
			GraphCanvas, _transform,
			_nodeOffsets, _nodeContainers, _basePositions, _boxHeights,
			_edgeRecords, _pendingEdges, _portCircles, _edgeToCircles, _circleToEdge,
			_pendingLabels, _pendingPortLabels);
		_interaction = new GraphInteraction(
			_transform, _tooltip, Scroll, Overlay,
			_nodeOffsets, _nodeContainers, _circleToEdge,
			updateConnectedEdges: _scene.UpdateConnectedEdges,
			redrawAfterDrag: _scene.RedrawAfterDrag,
			setPortHighlight: _scene.SetPortHighlight,
			getNodeMoved: () => NodeMoved,
			getOnNodeClicked: () => OnNodeClicked);
		_scene.AttachInteraction(_interaction);
		_diagnostics = new GraphDiagnostics(
			_transform, GraphCanvas,
			() => _scene.FirstLabeledEdge, () => _scene.LastModel,
			() => _nodeContainers, () => _boxHeights);

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

			_scene.PositionPendingLabels();
		};

		// Backup: the canvas's own layout pass (in case the scroll viewer's
		// LayoutUpdated fires before the labels have been measured).
		GraphCanvas.LayoutUpdated += (_, _) => _scene.PositionPendingLabels();
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
        _interaction.ClearPendingSelect();
    }

    /// <summary>
    /// Re-applies the theme-dependent brushes after a theme change: the
    /// selected node's accent ring (the edge-label masks are recreated on the
    /// next draw and pick up the refreshed <see cref="ThemeResources"/> cache
    /// then).
    /// </summary>
    public void RefreshThemeBrushes() => _interaction.RefreshThemeBrushes();

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode):
    /// the current content pan (the graph is positioned by a scale+translate
    /// transform, not the scroll offset).
    /// </summary>
    public Vector ScrollOffsetForVerification => _diagnostics.ScrollOffset;

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode):
    /// a one-line snapshot of the zoom/pan state for diagnostics.
    /// </summary>
    public string ScrollStateForVerification => _diagnostics.ScrollState;

    /// <summary>
    /// Verification helper (used by the --screenshot zoombug mode): reset
    /// the view to the natural size at the top-left (scale 1, pan 0), as
    /// the user would by fitting the graph back into the viewport.
    /// </summary>
    public void ScrollToOriginForVerification() => _diagnostics.ScrollToOrigin();

    /// <summary>
    /// Verification helper (used by the --screenshot mode): the center of
    /// the n-th node box in the given root's coordinate system (null if
    /// the boxes have not been laid out yet).
    /// </summary>
    public Point? GetNodeBoxCenter(Visual root, int n) => _diagnostics.GetNodeBoxCenter(root, n);

    /// <summary>
    /// Verification helper (used by the --screenshot interact mode): a point
    /// on the first edge that has a label (the midpoint of its bezier, in
    /// the given root's coordinate system), or null if no such edge exists.
    /// </summary>
    public Point? GetLabeledEdgePoint(Visual root) => _diagnostics.GetLabeledEdgePoint(root);

    /// <summary>
    /// A deterministic text fingerprint of the rendered scene (node layout
    /// positions, edge connections, and the transform state) for regression
    /// verification. Captured before a refactor and diffed after; a change
    /// indicates a rendering regression.
    /// </summary>
    public string GetSceneFingerprint() => _diagnostics.GetSceneFingerprint();

    /// <summary>
    /// Builds (or rebuilds) the graph for the given model. The node boxes,
    /// ports, edges, and arrowheads are created by the <see cref="GraphScene"/>;
    /// this method stores the model (for the post-drag redraw) and delegates.
    /// </summary>
    public void SetGraph(GraphModel.Result model, bool resetPan = true) => _scene.SetGraph(model, resetPan);

}
