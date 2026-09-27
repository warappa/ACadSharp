using System;
using System.Collections.Generic;
using System.Linq;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// The node graph's scene: the node boxes, the ports, and the layout
/// orchestration. Builds the node elements (the boxes, the port circles, the
/// port labels) into the given canvas, computes the layout (the node
/// positions, the box heights, the natural size), and drives the
/// <see cref="GraphEdgeFactory"/> to build the edges.
///
/// Reads and writes the shared element state
/// (<see cref="GraphElementState"/>). Each element's pointer events are wired
/// to the <see cref="GraphInteraction"/> (the shared interaction state and
/// logic), and the <see cref="GraphTransform"/> provides the zoom/pan state.
/// </summary>
public sealed class GraphScene
{
    // The canvas the elements are built into (the XAML GraphCanvas), the
    // zoom/pan transform, the interaction (for the pointer events), and the
    // edge factory (for building the edges).
    private readonly Canvas _canvas;
    private readonly GraphTransform _transform;
    private readonly GraphInteraction _interaction;
    private readonly GraphEdgeFactory _edgeFactory;

    // The shared element state (the node offsets / containers / base
    // positions / box heights / edge records / port-circle mappings and the
    // pending label + edge lists).
    private readonly GraphElementState _state;

    // The brushes the scene applies (the cross-highlight brush and the white
    // reset).
    private static readonly IBrush WhiteBrush = new SolidColorBrush(MediaColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush EdgeHoverBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));

    public GraphScene(
        Canvas canvas,
        GraphTransform transform,
        GraphInteraction interaction,
        GraphEdgeFactory edgeFactory,
        GraphElementState state)
    {
        _canvas = canvas;
        _transform = transform;
        _interaction = interaction;
        _edgeFactory = edgeFactory;
        _state = state;
    }

    /// <summary>
    /// The first edge that carried a label (the verification helper reads it).
    /// </summary>
    public Path? FirstLabeledEdge => _state.FirstLabeledEdge;

    /// <summary>
    /// The last rendered model (the verification helpers read it).
    /// </summary>
    public GraphModel.Result? LastModel => _state.LastModel;

    /// <summary>
    /// Rebuilds the scene for a new model: clears the element state, resets
    /// the pan, and rebuilds the node boxes, ports, edges, and arrowheads.
    /// </summary>
    public void SetGraph(GraphModel.Result model, bool resetPan)
    {
        _state.LastModel = model;
        _interaction.Reset();
        _transform.EndPan();
        if (resetPan)
        {
            _transform.ResetPan();
        }
        _state.PendingLabels.Clear();
        _state.FirstLabeledEdge = null;
        _state.PendingEdges.Clear();
        _state.PortCircles.Clear();
        _state.EdgeToCircles.Clear();
        _state.CircleToEdge.Clear();
        _state.NodeContainers.Clear();
        _state.EdgeRecords.Clear();
        _canvas.Children.Clear();

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
        _state.BasePositions.Clear();
        _state.BoxHeights.Clear();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            Point pos = GraphLayout.GetPosition(model, node);
            positions[node.Index] = pos;
            byIndex[node.Index] = node;
            _state.BasePositions[node.Index] = pos;
            _state.BoxHeights[node.Index] = GraphLayout.BoxHeightFor(node);
        }

        // Port positions: distributed evenly along the left/right edge of the box.
        // Apply the user-drag offset so ports follow the moved node.
        Dictionary<int, List<Point>> inputPortPos = new();
        Dictionary<int, List<Point>> outputPortPos = new();
        foreach (GraphNodeInfo node in model.Nodes)
        {
            Vector off = _state.NodeOffsets.GetValueOrDefault(node.Index);
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

            int srcIdx = GraphDrawing.FindPortIndex(fromNode.OutputPorts, edge.ToIndex, edge.WireIndex);
            int dstIdx = GraphDrawing.FindPortIndex(toNode.InputPorts, edge.FromIndex, edge.WireIndex);
            if (srcIdx < 0 || dstIdx < 0)
            {
                // The model and the view disagree on a port's (peer, wire)
                // identity — a real bug. Skip the edge rather than silently
                // anchoring it to port 0.
                Console.Error.WriteLine($"[graph] missing port for edge {edge.FromIndex}->{edge.ToIndex} wire {edge.WireIndex}");
                continue;
            }
            Point start = outputPortPos[edge.FromIndex][srcIdx];
            Point end = inputPortPos[edge.ToIndex][dstIdx];
            _edgeFactory.AddEdge(start, end, edge, fromNode, toNode);
        }

        // Node boxes.
        foreach (GraphNodeInfo node in model.Nodes)
        {
            AddNodeBox(node, positions[node.Index]);
        }

        // Edge labels last: above the node boxes, so a label wider than
        // the gap stays readable where it overlaps the source box.
        foreach (var (mask, _, _, _) in _state.PendingLabels)
        {
            _canvas.Children.Add(mask);
        }

        // Build the cross-highlighting mappings (edge ↔ port circle).
        foreach (var (line, fromIdx, toIdx, srcPortIdx, dstPortIdx) in _state.PendingEdges)
        {
            Ellipse? src = _state.PortCircles.TryGetValue((fromIdx, srcPortIdx, false), out var s) ? s : null;
            Ellipse? dst = _state.PortCircles.TryGetValue((toIdx, dstPortIdx, true), out var d) ? d : null;
            _state.EdgeToCircles[line] = (src, dst);
            if (src is not null) _state.CircleToEdge[src] = line;
            if (dst is not null) _state.CircleToEdge[dst] = line;
        }
        _state.PendingEdges.Clear();
        _state.PortCircles.Clear();

        (double naturalWidth, double naturalHeight) = GraphLayout.GetContentSize(model);
        _transform.SetNaturalSize(naturalWidth, naturalHeight);
        _transform.Apply();
    }

    /// <summary>
    /// Redraws the current model after a node drag (skipping the pan reset
    /// so the view doesn't jump).
    /// </summary>
    public void RedrawAfterDrag()
    {
        if (_state.LastModel is not null)
        {
            SetGraph(_state.LastModel, resetPan: false);
        }
    }

    /// <summary>
    /// Highlights or unhighlights the port circles connected to the given
    /// edge line (for cross-highlighting on edge hover).
    /// </summary>
    public void SetPortHighlight(Path line, bool highlight)
    {
        if (!_state.EdgeToCircles.TryGetValue(line, out var circles))
        {
            return;
        }

        IBrush brush = highlight ? EdgeHoverBrush : WhiteBrush;
        if (circles.src is not null) circles.src.Fill = brush;
        if (circles.dst is not null) circles.dst.Fill = brush;
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
        _state.NodeContainers[node.Index] = container;

        Vector offset = _state.NodeOffsets.GetValueOrDefault(node.Index);
        Canvas.SetLeft(container, position.X + offset.X);
        Canvas.SetTop(container, position.Y + offset.Y);
        _canvas.Children.Add(container);

        // Interaction: left-button drag moves the node; left click selects;
        // right-button propagates to the canvas for panning. The handlers
        // delegate to the GraphInteraction (the shared interaction state
        // and logic live there).
        border.PointerPressed += (_, e) => _interaction.OnNodePressed(border, node, isTarget, e);
        border.PointerMoved += (_, e) => _interaction.OnNodeMoved(border, node, position, e);
        border.PointerReleased += (_, e) => _interaction.OnNodeReleased(border, node, isTarget, e);
        border.PointerCaptureLost += (_, _) => _interaction.OnNodeCaptureLost(border);
        border.PointerEntered += (_, e) => _interaction.OnNodeEntered(border, node, isTarget, e);
        border.PointerExited += (_, _) => _interaction.OnNodeExited(border, node, isTarget);

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
            _state.PortCircles[(nodeIndex, i, isInput)] = circle;

            // Hover: highlight the circle and the connected edge.
            circle.PointerEntered += (_, e) => _interaction.OnPortEntered(circle, port, isInput, e);
            circle.PointerExited += (_, _) => _interaction.OnPortExited(circle, port, isInput);
            circle.PointerMoved += (_, e) => _interaction.OnPortMoved(circle, e);

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
                    Background = ThemeResources.CanvasBackground,
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
}
