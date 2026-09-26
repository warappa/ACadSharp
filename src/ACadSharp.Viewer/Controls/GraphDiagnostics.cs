using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// Read-only verification helpers for the node graph scene: the zoom/pan
/// state, the node box centers, the labeled-edge point, and the scene
/// fingerprint. Used by the --screenshot harness to verify the rendering
/// without vision. The mutable scene state (the node containers, the box
/// heights, the first labeled edge, the last model) is supplied via getters
/// so the diagnostics always read the current values.
/// </summary>
public class GraphDiagnostics
{
    private readonly GraphTransform _transform;
    private readonly Canvas _canvas;
    private readonly Func<Path?> _firstLabeledEdge;
    private readonly Func<GraphModel.Result?> _lastModel;
    private readonly Func<Dictionary<int, Canvas>> _nodeContainers;
    private readonly Func<Dictionary<int, double>> _boxHeights;

    public GraphDiagnostics(
        GraphTransform transform,
        Canvas canvas,
        Func<Path?> firstLabeledEdge,
        Func<GraphModel.Result?> lastModel,
        Func<Dictionary<int, Canvas>> nodeContainers,
        Func<Dictionary<int, double>> boxHeights)
    {
        _transform = transform;
        _canvas = canvas;
        _firstLabeledEdge = firstLabeledEdge;
        _lastModel = lastModel;
        _nodeContainers = nodeContainers;
        _boxHeights = boxHeights;
    }

    /// <summary>
    /// The current content pan (the graph is positioned by a scale+translate
    /// transform, not the scroll offset).
    /// </summary>
    public Vector ScrollOffset => _transform.ScrollOffset;

    /// <summary>
    /// A one-line snapshot of the zoom/pan state for diagnostics.
    /// </summary>
    public string ScrollState
    {
        get
        {
            Vector pan = _transform.ScrollOffset;
            Vector natural = _transform.NaturalSize;
            return $"scale={_transform.Scale:0.###} pan=({pan.X:0.##},{pan.Y:0.##}) " +
                $"canvas={_canvas.Bounds} natural={natural.X:0}x{natural.Y:0}";
        }
    }

    /// <summary>
    /// Reset the view to the natural size at the top-left (scale 1, pan 0),
    /// as the user would by fitting the graph back into the viewport.
    /// </summary>
    public void ScrollToOrigin() => _transform.ScrollToOrigin();

    /// <summary>
    /// The center of the n-th node box in the given root's coordinate system
    /// (null if the boxes have not been laid out yet). The boxes are the
    /// first Border child of the per-node container Canvases (the canvas's
    /// direct Border children are the edge-label masks, not node boxes).
    /// </summary>
    public Point? GetNodeBoxCenter(Visual root, int n)
    {
        int count = -1;
        foreach (Control child in _canvas.Children)
        {
            if (child is not Canvas container)
            {
                continue;
            }

            // The node box is the container's first Border child (added
            // before the port circles and labels).
            Border? box = null;
            foreach (Control c in container.Children)
            {
                if (c is Border b)
                {
                    box = b;
                    break;
                }
            }
            if (box is null || box.Bounds.Width <= 0)
            {
                continue;
            }

            count++;
            if (count != n)
            {
                continue;
            }

            return box.TranslatePoint(
                new Point(box.Bounds.Width / 2, box.Bounds.Height / 2),
                root);
        }

        return null;
    }

    /// <summary>
    /// A point on the first edge that has a label (the midpoint of its
    /// bezier, in the given root's coordinate system), or null if no such
    /// edge exists.
    /// </summary>
    public Point? GetLabeledEdgePoint(Visual root)
    {
        Path? line = _firstLabeledEdge();
        if (line is null
            || line.Data is not PathGeometry geometry
            || geometry.Figures is null || geometry.Figures.Count == 0)
        {
            return null;
        }

        var figure = geometry.Figures[0];
        if (figure.Segments is null || figure.Segments.Count == 0 || figure.Segments[0] is not BezierSegment bezier)
        {
            return null;
        }

        // The midpoint of the cubic bezier (t = 0.5):
        // (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3,
        // i.e. weights 1/8, 3/8, 3/8, 1/8.
        Point at = new Point(
            0.125 * figure.StartPoint.X + 0.375 * bezier.Point1.X
            + 0.375 * bezier.Point2.X + 0.125 * bezier.Point3.X,
            0.125 * figure.StartPoint.Y + 0.375 * bezier.Point1.Y
            + 0.375 * bezier.Point2.Y + 0.125 * bezier.Point3.Y);
        return line.TranslatePoint(at, root);
    }

    /// <summary>
    /// A deterministic text fingerprint of the rendered scene (node layout
    /// positions, edge connections, and the transform state) for regression
    /// verification. Captured before a refactor and diffed after; a change
    /// indicates a rendering regression. Node positions are in the canvas's
    /// content coordinate system (independent of the zoom/pan transform), so
    /// the layout part of the fingerprint is stable across transform changes.
    /// </summary>
    public string GetSceneFingerprint()
    {
        GraphModel.Result? model = _lastModel();
        if (model is null)
        {
            return "no model";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"nodes={model.Nodes.Count}");
        Dictionary<int, Canvas> containers = _nodeContainers();
        Dictionary<int, double> heights = _boxHeights();
        foreach (GraphNodeInfo node in model.Nodes.OrderBy(n => n.Index))
        {
            if (containers.TryGetValue(node.Index, out var container))
            {
                double x = Canvas.GetLeft(container);
                double y = Canvas.GetTop(container);
                double h = heights.GetValueOrDefault(node.Index, 0);
                sb.AppendLine($"  n[{node.Index}] {node.Kind} pos=({x:0.##},{y:0.##}) h={h:0.##}");
            }
            else
            {
                sb.AppendLine($"  n[{node.Index}] {node.Kind} pos=?");
            }
        }

        sb.AppendLine($"edges={model.Edges.Count}");
        foreach (GraphEdgeInfo edge in model.Edges)
        {
            sb.AppendLine($"  e from={edge.FromIndex} to={edge.ToIndex} label={edge.Label} dashed={edge.IsDashed} fb={edge.IsFeedback}");
        }

        Vector pan = _transform.ScrollOffset;
        Vector natural = _transform.NaturalSize;
        Rect visible = _transform.GetVisibleContentRect();
        sb.AppendLine($"scale={_transform.Scale:0.###} pan=({pan.X:0.##},{pan.Y:0.##}) natural={natural.X:0}x{natural.Y:0}");
        sb.AppendLine($"visible=({visible.X:0.##},{visible.Y:0.##},{visible.Width:0.##},{visible.Height:0.##})");

        return sb.ToString();
    }
}
