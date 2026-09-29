using System;
using System.Collections.Generic;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// The node graph's interaction: node selection (the accent ring + the
/// <c>OnNodeClicked</c> event), hover highlighting (the box ring, the port
/// circles, the edge arcs and their connected port circles), and node
/// dragging (the live edge update).
///
/// Holds the interaction state and the event-handler logic. It reads the
/// shared element state (<see cref="GraphElementState"/>, built by the
/// <see cref="GraphScene"/>) for the drag deltas, the container
/// repositioning, and the cross-highlighting. The behavior commands (update
/// the connected edges during a drag, redraw after a drag, cross-highlight
/// the port circles) are wired by the host via <see cref="Configure"/> once
/// the scene and the edge factory are built.
/// </summary>
public sealed class GraphInteraction
{
    // The shared element state (built by the scene; read here for the drag
    // deltas, the container repositioning, and the cross-highlighting).
    private readonly GraphElementState _state;

    // The transform (zoom/pan) and the hover tooltip.
    private readonly GraphTransform _transform;
    private readonly GraphTooltip _tooltip;

    // The coordinate contexts for pointer positions (the scroll viewport for
    // drag deltas, the overlay canvas for the floating tooltip).
    private readonly ScrollViewer _scroll;
    private readonly Canvas _overlay;

    // Behavior commands, wired by the host via Configure once the scene and
    // the edge factory are built: update the connected edges live during a
    // drag, redraw the whole graph after a drag (skipping the pan reset), and
    // cross-highlight the port circles connected to a hovered edge. (Set
    // after construction because they reference the scene / edge factory,
    // which are built after the interaction.)
    private Action<int> _updateConnectedEdges = null!;
    private Action _redrawAfterDrag = null!;
    private Action<Path, bool> _setPortHighlight = null!;

    // The control's events (getters so the interaction always invokes the
    // current handler, even if the host reassigns it after construction).
    private readonly Func<Action<int, Vector>?> _getNodeMoved;
    private readonly Func<Action<string>?> _getOnNodeClicked;

    // Interaction state: a box press first parks in _pendingSelectBox and
    // becomes a drag once the pointer moves past the drag threshold.
    private Border? _pendingSelectBox;
    private Point _pressPos;
    private Border? _hoverBox;
    private Border? _selectedBox;
    private bool _selectedIsTarget;
    private Border? _dragBox;
    private int _dragNodeIndex = -1;
    private Point _dragStartPos;
    private Point _dragLastPos;

    public const int DragThreshold = 4;

    public GraphInteraction(
        GraphTransform transform,
        GraphTooltip tooltip,
        ScrollViewer scroll,
        Canvas overlay,
        GraphElementState state,
        Func<Action<int, Vector>?> getNodeMoved,
        Func<Action<string>?> getOnNodeClicked)
    {
        _transform = transform;
        _tooltip = tooltip;
        _scroll = scroll;
        _overlay = overlay;
        _state = state;
        _getNodeMoved = getNodeMoved;
        _getOnNodeClicked = getOnNodeClicked;
    }

    /// <summary>
    /// Wires the behavior commands (called by the host once the scene and
    /// the edge factory are built): update the connected edges live during a
    /// drag, redraw the whole graph after a drag (skipping the pan reset),
    /// and cross-highlight the port circles connected to a hovered edge.
    /// </summary>
    public void Configure(
        Action<int> updateConnectedEdges,
        Action redrawAfterDrag,
        Action<Path, bool> setPortHighlight)
    {
        _updateConnectedEdges = updateConnectedEdges;
        _redrawAfterDrag = redrawAfterDrag;
        _setPortHighlight = setPortHighlight;
    }

    /// <summary>
    /// Clears the transient interaction state (called when the graph is
    /// redrawn, so a stale selection / hover / drag does not survive the
    /// rebuild).
    /// </summary>
    public void Reset()
    {
        _selectedBox = null;
        _selectedIsTarget = false;
        _hoverBox = null;
        _pendingSelectBox = null;
    }

    /// <summary>
    /// Drops the pending box press (a pan that just ended) without touching
    /// the selection / hover state.
    /// </summary>
    public void ClearPendingSelect()
    {
        _pendingSelectBox = null;
    }

    // The callbacks (the element pointer-event handlers).

    public void OnNodePressed(Border border, GraphNodeInfo node, bool isTarget, PointerPressedEventArgs e)
    {
        if (e.Properties.IsRightButtonPressed)
        {
            return; // let the canvas handle right-button pan
        }

        e.Handled = true;
        _pendingSelectBox = border;
        _pressPos = e.GetPosition(_scroll);
        _dragBox = border;
        _dragNodeIndex = node.Index;
        _dragStartPos = e.GetPosition(_scroll);
        _dragLastPos = e.GetPosition(_scroll);
        e.Pointer.Capture(border);
        border.Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    public void OnNodeMoved(Border border, GraphNodeInfo node, Point position, PointerEventArgs e)
    {
        if (_dragBox != border)
        {
            return;
        }

        Point pos = e.GetPosition(_scroll);
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

        Vector offset = _state.NodeOffsets.GetValueOrDefault(node.Index);
        _state.NodeOffsets[node.Index] = new Vector(offset.X + dx, offset.Y + dy);
        _getNodeMoved()?.Invoke(node.Index, _state.NodeOffsets[node.Index]);

        // Move the container (box + ports + labels move together).
        if (_state.NodeContainers.TryGetValue(node.Index, out var container))
        {
            Canvas.SetLeft(container, position.X + _state.NodeOffsets[node.Index].X);
            Canvas.SetTop(container, position.Y + _state.NodeOffsets[node.Index].Y);
        }

        // Update connected edges in real-time.
        _updateConnectedEdges(node.Index);
    }

    public void OnNodeReleased(Border border, GraphNodeInfo node, bool isTarget, PointerReleasedEventArgs e)
    {
        if (_dragBox == border)
        {
            _dragBox = null;

            if (_pendingSelectBox == border)
            {
                // Check if it was a drag or a click.
                Point pos = e.GetPosition(_scroll);
                bool wasDrag = Math.Abs(pos.X - _dragStartPos.X) > DragThreshold
                    || Math.Abs(pos.Y - _dragStartPos.Y) > DragThreshold;

                if (wasDrag)
                {
                    _pendingSelectBox = null;
                    // Redraw edges to follow the moved node.
                    // Skip pan reset so the view doesn't jump.
                    _redrawAfterDrag();
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
            _tooltip.ShowNode(node, e.GetPosition(_overlay));
        }
        else
        {
            _tooltip.Hide();
        }
    }

    public void OnNodeCaptureLost(Border border)
    {
        _pendingSelectBox = null;
        _transform.EndPan();
        border.Cursor = null;
    }

    public void OnNodeEntered(Border border, GraphNodeInfo node, bool isTarget, PointerEventArgs e)
    {
        if (_transform.IsPanning)
        {
            return; // the box is moving under the pointer while panning
        }

        _hoverBox = border;
        SetBoxBorder(border, isTarget, true);
        _tooltip.ShowNode(node, e.GetPosition(_overlay));
    }

    public void OnNodeExited(Border border, GraphNodeInfo node, bool isTarget)
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
        _tooltip.Hide();
    }

    public void OnPortEntered(Ellipse circle, PortInfo port, bool isInput, PointerEventArgs e)
    {
        circle.Fill = ThemeResources.Feedback;
        if (_state.CircleToEdge.TryGetValue(circle, out var edgeLine))
        {
            edgeLine.StrokeThickness = 3;
            edgeLine.Stroke = ThemeResources.Feedback;
        }
        _tooltip.ShowPort(port, isInput, e.GetPosition(_overlay));
    }

    public void OnPortExited(Ellipse circle, PortInfo port, bool isInput)
    {
        circle.Fill = ThemeResources.Port;
        if (_state.CircleToEdge.TryGetValue(circle, out var edgeLine))
        {
            edgeLine.StrokeThickness = 1.5;
            edgeLine.Stroke = ThemeResources.EdgeLine;
        }
        _tooltip.Hide();
    }

    public void OnPortMoved(Ellipse circle, PointerEventArgs e)
    {
        _tooltip.Move(e.GetPosition(_overlay));
    }

    public void OnEdgeLabelEntered(Border labelMask, Path line, Path? arrowhead, TextBlock label, GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode, PointerEventArgs e)
    {
        line.StrokeThickness = 3;
        line.Stroke = ThemeResources.Feedback;
        if (arrowhead is not null) arrowhead.Fill = ThemeResources.Feedback;
        label.Foreground = ThemeResources.Feedback;
        label.FontWeight = FontWeight.SemiBold;
        _setPortHighlight(line, true);
        _tooltip.ShowEdge(edge, fromNode, toNode, e.GetPosition(_overlay));
    }

    public void OnEdgeLabelExited(Border labelMask, Path line, Path? arrowhead, TextBlock label, GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode)
    {
        line.StrokeThickness = 1.5;
        line.Stroke = ThemeResources.EdgeLine;
        if (arrowhead is not null) arrowhead.Fill = ThemeResources.EdgeLine;
        label.Foreground = ThemeResources.EdgeLabel;
        label.FontWeight = FontWeight.Normal;
        _setPortHighlight(line, false);
        _tooltip.Hide();
    }

    public void OnEdgeLabelMoved(Border labelMask, PointerEventArgs e)
    {
        _tooltip.Move(e.GetPosition(_overlay));
    }

    public void OnEdgeEntered(Path line, Path? arrowhead, TextBlock? label, GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode, PointerEventArgs e)
    {
        line.StrokeThickness = 3;
        line.Stroke = ThemeResources.Feedback;
        if (arrowhead is not null)
        {
            arrowhead.Fill = ThemeResources.Feedback;
        }
        if (label is not null)
        {
            label.Foreground = ThemeResources.Feedback;
            label.FontWeight = FontWeight.SemiBold;
        }
        _setPortHighlight(line, true);
        _tooltip.ShowEdge(edge, fromNode, toNode, e.GetPosition(_overlay));
    }

    public void OnEdgeExited(Path line, Path? arrowhead, TextBlock? label, GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode)
    {
        line.StrokeThickness = 1.5;
        line.Stroke = ThemeResources.EdgeLine;
        if (arrowhead is not null)
        {
            arrowhead.Fill = ThemeResources.EdgeLine;
        }
        if (label is not null)
        {
            label.Foreground = ThemeResources.EdgeLabel;
            label.FontWeight = FontWeight.Normal;
        }
        _setPortHighlight(line, false);
        _tooltip.Hide();
    }

    public void OnEdgeMoved(Path line, PointerEventArgs e)
    {
        _tooltip.Move(e.GetPosition(_overlay));
    }

    // The feedback arc: on hover it only thickens (the arc keeps its
    // Feedback token stroke — no color change), cross-highlights the
    // connected port circles, and shows the tooltip.
    public void OnFeedbackEdgeEntered(Path line, GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode, PointerEventArgs e)
    {
        line.StrokeThickness = 3;
        _setPortHighlight(line, true);
        _tooltip.ShowEdge(edge, fromNode, toNode, e.GetPosition(_overlay));
    }

    public void OnFeedbackEdgeExited(Path line, GraphEdgeInfo edge, GraphNodeInfo? fromNode, GraphNodeInfo? toNode)
    {
        line.StrokeThickness = 1.5;
        _setPortHighlight(line, false);
        _tooltip.Hide();
    }

    // The interaction methods.

    /// <summary>
    /// Selects a node box: accent ring + the <c>OnNodeClicked</c> event.
    /// </summary>
    public void SelectNode(Border border, GraphNodeInfo node, bool isTarget)
    {
        if (_selectedBox != null)
        {
            SetBoxBorder(_selectedBox, _selectedIsTarget, _hoverBox == _selectedBox);
        }

        _selectedBox = border;
        _selectedIsTarget = isTarget;
        SetBoxBorder(border, isTarget, _hoverBox == border);
        _getOnNodeClicked()?.Invoke(GraphDrawing.BuildTooltip(node));
    }

    /// <summary>
    /// Applies the border for a box's current state: the accent selection
    /// ring wins, then the hover highlight (the token white / gray ring),
    /// then the default (the target ring, invisible otherwise). The thickness
    /// is constant per box (3 for the target, 2 otherwise) so the inner text
    /// never shifts when the hover state changes — only the brush changes
    /// (transparent = invisible but still reserves the border slot).
    /// </summary>
    public void SetBoxBorder(Border border, bool isTarget, bool hovered)
    {
        border.BorderThickness = new Thickness(isTarget ? 3 : 2);

        if (_selectedBox == border)
        {
            border.BorderBrush = GraphLayout.GetAccentBrush();
            return;
        }

        if (hovered)
        {
            // The target's hover ring takes the strong canvas color (Port);
            // the other nodes take the softer hover gray. Both are readable
            // against the canvas in their theme.
            border.BorderBrush = isTarget ? ThemeResources.Port : ThemeResources.NodeHover;
            return;
        }

        border.BorderBrush = isTarget
            ? ThemeResources.NodeTargetStroke
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
}
