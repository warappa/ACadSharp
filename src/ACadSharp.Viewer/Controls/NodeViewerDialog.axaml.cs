using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using Avalonia.Controls;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// Modal dialog showing the subgraph of nodes involved in constructing a
/// property's value: the target node plus its upstream ancestors
/// (transitive closure over incoming edges), laid out left to right.
/// Clicking a node shows its full details in the bottom panel.
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
    }
}
