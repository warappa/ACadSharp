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
/// The node graph's scene: the element tree and the construction that
/// builds it. Owns the element state (the node offsets / containers /
/// base positions / box heights / edge records / port-circle mappings and
/// the pending label + edge lists) and the last rendered model, and builds
/// the elements (node boxes, ports, edges, arrowheads) into the given
/// canvas.
///
/// The construction wires each element's pointer events to the
/// <see cref="GraphInteraction"/> (set after construction via
/// <see cref="AttachInteraction"/>) and uses the <see cref="GraphTransform"/>
/// for the zoom/pan state. The element state is shared with the control
/// (which owns the dictionary instances) and the interaction.
/// </summary>
public sealed class GraphScene
{
    // The canvas the elements are built into (the XAML GraphCanvas) and the
    // zoom/pan transform.
    private readonly Canvas _canvas;
    private readonly GraphTransform _transform;

    // The interaction (set after construction so the shared element state can
    // be passed to both without a circular-construction dependency).
    private GraphInteraction _interaction = null!;

    // The element state (shared with the control and the interaction).
    private readonly Dictionary<int, Vector> _nodeOffsets;
    private readonly Dictionary<int, Canvas> _nodeContainers;
    private readonly Dictionary<int, Point> _basePositions;
    private readonly Dictionary<int, double> _boxHeights;
    private readonly List<(Path line, Path? arrowhead, Border? labelMask, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> _edgeRecords;
    private readonly List<(Path line, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> _pendingEdges;
    private readonly Dictionary<(int nodeIdx, int portIdx, bool isInput), Ellipse> _portCircles;
    private readonly Dictionary<Path, (Ellipse? src, Ellipse? dst)> _edgeToCircles;
    private readonly Dictionary<Ellipse, Path> _circleToEdge;
    private readonly List<(Border mask, TextBlock label, double startX, double endX)> _pendingLabels;
    private readonly List<Border> _pendingPortLabels;

    // The last rendered model and the first edge that carried a label (for
    // the verification helpers).
    private GraphModel.Result? _lastModel;
    private Path? _firstLabeledEdge;

    public GraphScene(
        Canvas canvas,
        GraphTransform transform,
        Dictionary<int, Vector> nodeOffsets,
        Dictionary<int, Canvas> nodeContainers,
        Dictionary<int, Point> basePositions,
        Dictionary<int, double> boxHeights,
        List<(Path line, Path? arrowhead, Border? labelMask, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> edgeRecords,
        List<(Path line, int fromIdx, int toIdx, int srcPortIdx, int dstPortIdx)> pendingEdges,
        Dictionary<(int nodeIdx, int portIdx, bool isInput), Ellipse> portCircles,
        Dictionary<Path, (Ellipse? src, Ellipse? dst)> edgeToCircles,
        Dictionary<Ellipse, Path> circleToEdge,
        List<(Border mask, TextBlock label, double startX, double endX)> pendingLabels,
        List<Border> pendingPortLabels)
    {
        _canvas = canvas;
        _transform = transform;
        _nodeOffsets = nodeOffsets;
        _nodeContainers = nodeContainers;
        _basePositions = basePositions;
        _boxHeights = boxHeights;
        _edgeRecords = edgeRecords;
        _pendingEdges = pendingEdges;
        _portCircles = portCircles;
        _edgeToCircles = edgeToCircles;
        _circleToEdge = circleToEdge;
        _pendingLabels = pendingLabels;
        _pendingPortLabels = pendingPortLabels;
    }

    /// <summary>
    /// Wires the interaction (called by the control once the interaction is
    /// built, so the construction can reach it without a circular
    /// construction dependency).
    /// </summary>
    public void AttachInteraction(GraphInteraction interaction)
    {
        _interaction = interaction;
    }

    public Path? FirstLabeledEdge => _firstLabeledEdge;
    public GraphModel.Result? LastModel => _lastModel;

    /// <summary>
    /// Rebuilds the scene for a new model: clears the element state, resets
    /// the pan, and rebuilds the node boxes, ports, edges, and arrowheads.
    /// </summary>
    public void SetGraph(GraphModel.Result model, bool resetPan)
    {
        _lastModel = model;
        _interaction.Reset();
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
            _canvas.Children.Add(mask);
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

    /// <summary>
    /// Redraws the current model after a node drag (skipping the pan reset
    /// so the view doesn't jump).
    /// </summary>
    public void RedrawAfterDrag()
    {
        if (_lastModel is not null)
        {
            SetGraph(_lastModel, resetPan: false);
        }
    }

    /// <summary>
    /// Recomputes the geometry of all edges connected to the given node,
    /// using the node's current (offset) position. Called during a drag
    /// so the arrows follow the moved node in real-time.
    /// </summary>
    public void UpdateConnectedEdges(int nodeIndex)
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

    /// <summary>
    /// Parks each pending edge label at the edge midpoint, shifted left so
    /// its right edge stays at least 16px clear of the arrowhead tip
    /// (the 8px arrowhead plus 8px of visible gap), once the text width is
    /// known. For long labels on short edges that shifts the label left
    /// over the source box's edge — the labels draw above the boxes, so
    /// the overlap stays readable. Labels that are not measured yet are
    /// left for the next layout pass.
    /// </summary>
    public void PositionPendingLabels()
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
    /// Highlights or unhighlights the port circles connected to the given
    /// edge line (for cross-highlighting on edge hover).
    /// </summary>
    public void SetPortHighlight(Path line, bool highlight)
    {
        if (!_edgeToCircles.TryGetValue(line, out var circles))
        {
            return;
        }

        IBrush brush = highlight ? EdgeHoverBrush : WhiteBrush;
        if (circles.src is not null) circles.src.Fill = brush;
        if (circles.dst is not null) circles.dst.Fill = brush;
    }

    private static readonly IBrush WhiteBrush = new SolidColorBrush(MediaColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    private static readonly IBrush EdgeLineBrush = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A));
    private static readonly IBrush EdgeLabelBrush = new SolidColorBrush(MediaColor.FromArgb(0xE0, 0x80, 0x80, 0x80));
    private static readonly IBrush EdgeHoverBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
    private static readonly IBrush FeedbackBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
    private static readonly IBrush FeedbackLabelBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));

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
            _portCircles[(nodeIndex, i, isInput)] = circle;

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
            line.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 4 };
        }

        _canvas.Children.Add(line);

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
                Background = ThemeResources.CanvasBackground,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 1),
                Child = label,
            };
            // Hovering the label highlights the edge (same as hovering the line).
            labelMask.PointerEntered += (_, e) => _interaction.OnEdgeLabelEntered(labelMask, line, arrowhead, label!, edge, fromNode, toNode, e);
            labelMask.PointerExited += (_, _) => _interaction.OnEdgeLabelExited(labelMask, line, arrowhead, label!, edge, fromNode, toNode);
            labelMask.PointerMoved += (_, e) => _interaction.OnEdgeLabelMoved(labelMask, e);
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
        line.PointerEntered += (_, e) => _interaction.OnEdgeEntered(line, arrowhead, label, edge, fromNode, toNode, e);
        line.PointerExited += (_, _) => _interaction.OnEdgeExited(line, arrowhead, label, edge, fromNode, toNode);
        line.PointerMoved += (_, e) => _interaction.OnEdgeMoved(line, e);
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
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 4 },
        };
        _canvas.Children.Add(line);

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
                Background = ThemeResources.CanvasBackground,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 1),
                Child = label,
                IsHitTestVisible = false,
            };
            // Position the label at the apex, centered horizontally.
            double labelWidth = edge.Label.Length * 7; // rough estimate
            Canvas.SetLeft(fbLabelMask, apexX - labelWidth / 2);
            Canvas.SetTop(fbLabelMask, apexY - 12);
            _canvas.Children.Add(fbLabelMask);
        }
        _edgeRecords.Add((line, fbArrowhead, fbLabelMask, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

        // Hover: thicken the arc, highlight connected port circles, and show a tooltip.
        line.PointerEntered += (_, e) => _interaction.OnFeedbackEdgeEntered(line, edge, fromNode, toNode, e);
        line.PointerExited += (_, _) => _interaction.OnFeedbackEdgeExited(line, edge, fromNode, toNode);
        line.PointerMoved += (_, e) => _interaction.OnEdgeMoved(line, e);
    }

    private Path AddArrowhead(Point at, Vector direction) => AddArrowhead(at, direction, EdgeLineBrush);

    private Path AddArrowhead(Point at, Vector direction, IBrush brush)
    {
        var arrowhead = new Path
        {
            Data = GraphDrawing.BuildArrowheadGeometry(at, direction),
            Fill = brush,
        };
        _canvas.Children.Add(arrowhead);
        return arrowhead;
    }

    private int GetPortCount(int nodeIndex, bool isInput)
    {
        if (_lastModel is null) return 1;
        var node = _lastModel.Nodes.FirstOrDefault(n => n.Index == nodeIndex);
        return isInput ? Math.Max(1, node?.InputPorts.Count ?? 1) : Math.Max(1, node?.OutputPorts.Count ?? 1);
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
}
