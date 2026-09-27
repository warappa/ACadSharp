using System;
using System.Collections.Generic;
using System.Linq;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// Builds the node graph's edges: the bezier arcs, the feedback (dashed,
/// orange) arcs, the arrowheads, and the edge labels.
///
/// Reads the shared element state (<see cref="GraphElementState"/>) for the
/// live update (the geometry of the edges connected to a dragged node) and
/// writes the built edges (the records, the pending edges / labels, and the
/// first labeled edge). Wires each edge's pointer events to the
/// <see cref="GraphInteraction"/> (the shared interaction state and logic).
/// </summary>
public sealed class GraphEdgeFactory
{
    // The canvas the edges are built into (the XAML GraphCanvas) and the
    // interaction (for the pointer events).
    private readonly Canvas _canvas;
    private readonly GraphInteraction _interaction;

    // The shared element state (the node offsets / base positions / box
    // heights / edge records / pending edges + labels / first labeled edge).
    private readonly GraphElementState _state;

    // The edge brushes (hardcoded, theme-independent; the label masks use
    // ThemeResources.CanvasBackground).
    private static readonly IBrush EdgeLineBrush = new SolidColorBrush(MediaColor.FromArgb(0xB0, 0x9A, 0x9A, 0x9A));
    private static readonly IBrush EdgeLabelBrush = new SolidColorBrush(MediaColor.FromArgb(0xE0, 0x80, 0x80, 0x80));
    private static readonly IBrush FeedbackBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));
    private static readonly IBrush FeedbackLabelBrush = new SolidColorBrush(MediaColor.Parse("#E8A33D"));

    public GraphEdgeFactory(Canvas canvas, GraphInteraction interaction, GraphElementState state)
    {
        _canvas = canvas;
        _interaction = interaction;
        _state = state;
    }

    /// <summary>
    /// Builds a regular edge: a bezier arc from the source's output port to
    /// the target's input port, with an arrowhead at the target end and (for
    /// labeled edges) a label at the midpoint.
    /// </summary>
    public void AddEdge(
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

        _canvas.Children.Add(line);

        // Record for cross-highlighting (edge ↔ port circle).
        int srcPortIdx = fromNode is not null ? GraphDrawing.FindPortIndex(fromNode.OutputPorts, edge.ToIndex, edge.WireIndex) : 0;
        int dstPortIdx = toNode is not null ? GraphDrawing.FindPortIndex(toNode.InputPorts, edge.FromIndex, edge.WireIndex) : 0;
        _state.PendingEdges.Add((line, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

        // Arrowhead at the target end (pointing along the edge direction).
        Path? arrowhead = null;
        if (hasDirection)
        {
            arrowhead = AddArrowhead(end, direction);
        }
        // Label at the midpoint; the opaque background mask (the canvas
        // background color) keeps the label readable where it overlaps the
        // edge line. The label is added to the canvas AFTER the node boxes
        // (see GraphScene.SetGraph), so a label that is wider than the gap
        // stays readable as a badge over the source box's edge instead of
        // being hidden behind it. The final position is refined after layout,
        // once the text width is known: centered on the edge midpoint,
        // shifted left so the right edge stays at least 16px clear of the
        // arrowhead tip.
        TextBlock? label = null;
        Border? labelMask = null;
        // Suppress the per-wire edge label when the edge fans out (WireCount
        // > 1): the port labels (next to the circles) already name each
        // wire, so a per-wire edge label would just crowd the gap between
        // the nodes.
        if (edge.Label.Length > 0 && edge.WireCount == 1)
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

            _state.PendingLabels.Add((labelMask, label, start.X, end.X));
            // NOTE: the mask is added to the canvas in GraphScene.SetGraph,
            // after the node boxes (so it draws above them).
            if (_state.FirstLabeledEdge is null)
            {
                _state.FirstLabeledEdge = line;
            }
        }
        _state.EdgeRecords.Add((line, arrowhead, labelMask, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

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
    public void AddFeedbackEdge(
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
        _canvas.Children.Add(line);

        // Record for cross-highlighting (edge ↔ port circle).
        int srcPortIdx = fromNode is not null ? GraphDrawing.FindPortIndex(fromNode.OutputPorts, edge.ToIndex, edge.WireIndex) : 0;
        int dstPortIdx = toNode is not null ? GraphDrawing.FindPortIndex(toNode.InputPorts, edge.FromIndex, edge.WireIndex) : 0;
        _state.PendingEdges.Add((line, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

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
        // Suppress the per-wire edge label when the edge fans out (see
        // AddEdge); the port labels already name each wire.
        if (edge.Label.Length > 0 && edge.WireCount == 1)
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
        _state.EdgeRecords.Add((line, fbArrowhead, fbLabelMask, edge.FromIndex, edge.ToIndex, srcPortIdx, dstPortIdx));

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

    /// <summary>
    /// Recomputes the geometry of all edges connected to the given node,
    /// using the node's current (offset) position. Called during a drag
    /// so the arrows follow the moved node in real-time.
    /// </summary>
    public void UpdateConnectedEdges(int nodeIndex)
    {
        foreach (var (line, arrowhead, labelMask, fromIdx, toIdx, srcPortIdx, dstPortIdx) in _state.EdgeRecords)
        {
            if (fromIdx != nodeIndex && toIdx != nodeIndex)
            {
                continue;
            }

            // Compute the current port positions (base + offset).
            if (!_state.BasePositions.TryGetValue(fromIdx, out Point fromBase)
                || !_state.BasePositions.TryGetValue(toIdx, out Point toBase)
                || !_state.BoxHeights.TryGetValue(fromIdx, out double fromH)
                || !_state.BoxHeights.TryGetValue(toIdx, out double toH))
            {
                continue;
            }

            Vector fromOff = _state.NodeOffsets.GetValueOrDefault(fromIdx);
            Vector toOff = _state.NodeOffsets.GetValueOrDefault(toIdx);

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
        if (_state.PendingLabels.Count == 0)
        {
            return;
        }

        for (int i = _state.PendingLabels.Count - 1; i >= 0; i--)
        {
            (Border mask, TextBlock label, double startX, double endX) = _state.PendingLabels[i];
            double w = label.DesiredSize.Width;
            if (w <= 0)
            {
                continue; // not measured yet; try on the next pass
            }

            double centeredLeft = (startX + endX) / 2 - w / 2;
            double labelLeft = Math.Min(centeredLeft, endX - 16 - w);
            Canvas.SetLeft(mask, labelLeft - 2); // -2 = mask left padding
            _state.PendingLabels.RemoveAt(i);
        }
    }

    /// <summary>
    /// The port count of the given node (input or output), at least 1.
    /// </summary>
    private int GetPortCount(int nodeIndex, bool isInput)
    {
        if (_state.LastModel is null) return 1;
        var node = _state.LastModel.Nodes.FirstOrDefault(n => n.Index == nodeIndex);
        return isInput ? Math.Max(1, node?.InputPorts.Count ?? 1) : Math.Max(1, node?.OutputPorts.Count ?? 1);
    }
}
