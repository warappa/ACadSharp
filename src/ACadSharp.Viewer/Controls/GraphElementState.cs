using System.Collections.Generic;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// The shared element state of the node graph. The <see cref="GraphScene"/>
/// builds it (the node boxes, ports, edges, and arrowheads); the
/// <see cref="GraphInteraction"/> reads it (the drag offsets, the containers,
/// and the cross-highlighting maps); and the <see cref="GraphEdgeFactory"/>
/// builds the edges into it.
///
/// A single object so the scene and the edge factory can share the state
/// without depending on each other (which would form a circular-construction
/// dependency). The host (the control) creates it and passes it to all three.
/// </summary>
public sealed class GraphElementState
{
    // Node state. NodeOffsets hold the user-drag offsets and PERSIST across
    // rebuilds (a dragged node stays where it was dropped); the containers
    // and the base geometry are rebuilt every time.
    public Dictionary<int, Vector> NodeOffsets { get; } = new();
    public Dictionary<int, Canvas> NodeContainers { get; } = new();
    public Dictionary<int, Point> BasePositions { get; } = new();
    public Dictionary<int, double> BoxHeights { get; } = new();

    // Edge state. EdgeRecords are the built edges (for the live update during
    // a drag); the pending lists are the edges / labels not yet finalized.
    public List<(Path line, Path? arrowhead, Border? labelMask, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> EdgeRecords { get; } = new();
    public List<(Path line, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> PendingEdges { get; } = new();
    public List<(Border mask, TextBlock label, double startX, double endX)> PendingLabels { get; } = new();

    // Cross-highlighting: the port circles and the edge ↔ port-circle maps.
    public Dictionary<(int nodeIdx, int portIdx, bool isInput), Ellipse> PortCircles { get; } = new();
    public Dictionary<Path, (Ellipse? src, Ellipse? dst)> EdgeToCircles { get; } = new();
    public Dictionary<Ellipse, Path> CircleToEdge { get; } = new();

    // Pending port labels (the opaque badges beside the port circles).
    public List<Border> PendingPortLabels { get; } = new();

    // The last rendered model and the first edge that carried a label (the
    // verification helpers read these).
    public GraphModel.Result? LastModel { get; set; }
    public Path? FirstLabeledEdge { get; set; }
}
