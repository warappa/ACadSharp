using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

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

    // The scene: builds the node boxes and owns the layout (the node
    // positions, the box heights, the natural size). It reads/writes the
    // shared element state below and drives the edge factory.
    private GraphScene _scene = null!;

    // The edge factory: builds the edges (the bezier arcs, the feedback arcs,
    // the arrowheads, the edge labels). Shares the element state with the
    // scene.
    private GraphEdgeFactory _edgeFactory = null!;

    // The shared element state: the node offsets (the user-drag offsets, which
    // persist across rebuilds), the node containers, the base positions, the
    // box heights, the edge records, the pending edges / labels, and the
    // port-circle cross-highlighting mappings. The scene builds it; the
    // interaction and the edge factory read it.
    private readonly GraphElementState _state = null!;

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

		// The shared element state (the node offsets / containers / edge
		// records / port-circle mappings). The scene builds it; the interaction
		// and the edge factory read it.
		_state = new GraphElementState();

		// 1. The interaction is created first. Its behavior commands (update the
		//    connected edges, redraw after a drag, cross-highlight the port
		//    circles) reference the scene / edge factory, which are built
		//    below, so they are wired via Configure after construction.
		_interaction = new GraphInteraction(
			_transform, _tooltip, Scroll, Overlay,
			_state,
			getNodeMoved: () => NodeMoved,
			getOnNodeClicked: () => OnNodeClicked);

		// 2. The edge factory builds the edges (it takes the interaction to
		//    wire the edges' pointer events).
		_edgeFactory = new GraphEdgeFactory(GraphCanvas, _interaction, _state);

		// 3. The scene builds the node boxes and owns the layout (it takes the
		//    interaction and the edge factory).
		_scene = new GraphScene(GraphCanvas, _transform, _interaction, _edgeFactory, _state);

		// 4. Wire the interaction's behavior commands to the scene / edge
		//    factory.
		_interaction.Configure(
			updateConnectedEdges: _edgeFactory.UpdateConnectedEdges,
			redrawAfterDrag: _scene.RedrawAfterDrag,
			setPortHighlight: _scene.SetPortHighlight);

		// 5. The diagnostic readout.
		_diagnostics = new GraphDiagnostics(
			_transform, GraphCanvas,
			() => _scene.FirstLabeledEdge, () => _scene.LastModel,
			() => _state.NodeContainers, () => _state.BoxHeights);

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

			_edgeFactory.PositionPendingLabels();
		};

		// Backup: the canvas's own layout pass (in case the scroll viewer's
		// LayoutUpdated fires before the labels have been measured).
		GraphCanvas.LayoutUpdated += (_, _) => _edgeFactory.PositionPendingLabels();
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
