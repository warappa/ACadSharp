using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// Modal dialog showing the subgraph of nodes involved in constructing a
/// property's value: the target node (highlighted ring) plus its upstream
/// ancestors (transitive closure over incoming edges), laid out left to
/// right. The header carries color legends (node types and line types) and
/// fit/zoom controls.
/// Clicking a node selects it (accent ring) and shows its full details in
/// the bottom panel; hovering a node or edge shows a floating summary;
/// the wheel zooms about the cursor and dragging pans the graph. A
/// mini-map in the bottom-right corner always shows the whole graph, with
/// a rectangle tracking the visible area (pressing or dragging it
/// re-centers the view).
/// </summary>
public partial class NodeViewerDialog : Window
{
	private readonly BlockRecord _block;

	private readonly PropertyItem _property;

	private readonly EvaluationGraph _graph;

	public NodeViewerDialog(BlockRecord block, PropertyItem property, EvaluationGraph graph)
	{
		_block = block;
		_property = property;
		_graph = graph;

		InitializeComponent();
		InitializeComponentState();
	}

	private void InitializeComponentState()
	{
		Header.Text = $"Nodes building '{_property.Name}' — block {_block.Name}";

		GraphModel.Result model = GraphModel.BuildAncestors(_graph, _property.NodeIndex);
		// The mini-map gets the graph first, so the ViewChanged events the
		// main view raises while setting its graph are already handled.
		MiniMap.SetGraph(model);
		Graph.SetGraph(model);
		Graph.OnNodeClicked = details => Details.Text = details;

		// The mini-map's visible-area rectangle follows the main view's
		// transform; pressing or dragging the mini-map re-centers the main
		// view on the content point under the pointer; a dragged node
		// follows in the overview.
		Graph.ViewChanged += rect => MiniMap.SetViewRect(rect);
		Graph.NodeMoved += (index, offset) => MiniMap.SetNodeOffset(index, offset);
		MiniMap.Navigate += p => Graph.CenterOnContentPoint(p);

		// Fit once the layout pass has measured the scroll view.
		Dispatcher.UIThread.Post(() => Graph.FitToView(), DispatcherPriority.Background);
	}

    /// <summary>
    /// Re-applies the theme-dependent brushes (the graph's accent ring and the
    /// mini-map card) after a theme variant or accent change; the host calls
    /// this so an open dialog tracks the theme.
    /// </summary>
    public void RefreshThemeBrushes()
    {
        Graph.RefreshThemeBrushes();
        MiniMap.RefreshThemeBrushes();
    }

	private void OnFitClick(object? sender, RoutedEventArgs e) => Graph.FitToView();

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => Graph.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => Graph.ZoomOut();
}
