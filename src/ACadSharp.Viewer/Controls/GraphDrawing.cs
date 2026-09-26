using System;
using System.Text;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Media;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// Pure drawing helpers for the node graph: the edge/arrowhead geometry and
/// the node name/tooltip formatting. No state, no side effects — the host
/// (<see cref="NodeGraphView"/>) creates the visual elements and adds them to
/// the canvas, so the geometry and text can be built, and unit-tested, in
/// isolation.
/// </summary>
public static class GraphDrawing
{
    /// <summary>
    /// A filled triangle pointing along <paramref name="direction"/> (the
    /// arrowhead at the target end of an edge).
    /// </summary>
    public static Geometry BuildArrowheadGeometry(Point at, Vector direction)
    {
        double size = 8;
        Vector normal = new Vector(-direction.Y, direction.X);
        Point p1 = at - direction * size;
        Point p2 = p1 + normal * (size / 2);
        Point p3 = p1 - normal * (size / 2);
        var geo = new PathGeometry();
        var fig = new PathFigure
        {
            StartPoint = at,
            IsClosed = true,
        };
        fig.Segments!.Add(new LineSegment { Point = p2 });
        fig.Segments!.Add(new LineSegment { Point = p3 });
        geo.Figures!.Add(fig);
        return geo;
    }

    /// <summary>
    /// A cubic bezier from <paramref name="start"/> to <paramref name="end"/>
    /// with a horizontal control-point offset, shortened by 5px at the target
    /// end (so the arrowhead tip meets the port).
    /// </summary>
    public static Geometry BuildEdgeGeometry(Point start, Point end)
    {
        Vector direction = end - start;
        bool hasDirection = direction.SquaredLength > 0.01;
        if (hasDirection) direction = direction.Normalize();
        Point lineEnd = hasDirection ? end - direction * 5 : end;
        double dx = Math.Max(Math.Abs(end.X - start.X) * 0.5, 40);
        var geo = new PathGeometry();
        var fig = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
        };
        fig.Segments!.Add(new BezierSegment
        {
            Point1 = new Point(start.X + dx, start.Y),
            Point2 = new Point(lineEnd.X - dx, lineEnd.Y),
            Point3 = lineEnd,
        });
        geo.Figures!.Add(fig);
        return geo;
    }

    /// <summary>
    /// The display name of a node's element: the block element name
    /// (parameters, grips, and actions all carry one); null when the element
    /// has no name (e.g. grip location components).
    /// </summary>
    public static string? GetName(EvaluationExpression expression)
    {
        string? name = (expression as BlockElement)?.ElementName;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// A multi-line dump of a node's element (type / name / value / id, plus
    /// the grip location and displacement when it is a grip) — the node's
    /// click details and hover tooltip.
    /// </summary>
    public static string BuildTooltip(GraphNodeInfo node)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{node.Expression.GetType().Name}  (node #{node.Index}, {node.Kind})");
        string? name = GetName(node.Expression);
        if (name is not null)
        {
            sb.AppendLine($"name: {name}");
        }
        sb.AppendLine($"value: {ValueFormatter.Format(node.Expression)}");
        sb.AppendLine($"id: {node.Expression.Id}");

        if (node.Expression is BlockGrip grip)
        {
            sb.AppendLine($"location: {grip.Location}  displacement: {grip.Displacement}");
        }

        return sb.ToString();
    }
}
