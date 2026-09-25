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
/// right. The header carries a color legend and fit/zoom controls.
/// Clicking a node selects it (accent ring) and shows its full details in
/// the bottom panel; hovering a node or edge shows a floating summary;
/// the wheel zooms about the cursor and dragging pans the graph.
/// </summary>
public partial class NodeViewerDialog : Window
{
    public NodeViewerDialog(BlockRecord block, PropertyItem property, EvaluationGraph graph)
    {
        InitializeComponent();

        Header.Text = $"Nodes building '{property.Name}' — block {block.Name}";

        GraphModel.Result model = GraphModel.BuildAncestors(graph, property.NodeIndex);
        Graph.SetGraph(model);
        Graph.OnNodeClicked = details => Details.Text = details;

        // Fit once the layout pass has measured the scroll view.
        Dispatcher.UIThread.Post(() => Graph.FitToView(), DispatcherPriority.Background);
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => Graph.FitToView();

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => Graph.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => Graph.ZoomOut();
}
