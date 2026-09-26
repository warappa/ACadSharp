using ACadSharp.Objects.Evaluations;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// Shared layout metrics and color rules for the node graph and its mini-map.
/// Both <c>NodeGraphView</c> and <c>MiniMapView</c> draw the same layout
/// (columns by BFS depth, rows within a column), so the content bounds must be
/// identical for the mini-map's visible-area rectangle to line up. Keeping the
/// constants and the kind-color / box-height / accent rules in one place
/// guarantees the two views stay in sync.
/// </summary>
public static class GraphLayout
{
    public const double BoxWidth = 170;
    public const double BoxHeight = 48;

    // A named node shows three lines (name / type / value), so its box is
    // taller than the two-line boxes.
    public const double NamedBoxHeight = 64;

    // Column pitch: the horizontal gap between node boxes is ColumnWidth -
    // BoxWidth (150px). The edge labels sit in that gap (centered on the
    // line, 16px clear of the arrowhead), so the pitch must leave room for
    // the longest port-name label (e.g. "Displacement lookup ×2", ~120px).
    public const double ColumnWidth = 320;
    public const double RowHeight = 80;

    // Inset of the first/last column and row from the content edge.
    public const double LayoutMargin = 20;

    /// <summary>
    /// The height of a node's box: named nodes show three lines (name / type /
    /// value) and get the taller box; unnamed nodes stay at the two-line
    /// height.
    /// </summary>
    public static double BoxHeightFor(GraphNodeInfo? node)
    {
        string? name = (node?.Expression as BlockElement)?.ElementName;
        return string.IsNullOrWhiteSpace(name) ? BoxHeight : NamedBoxHeight;
    }

    /// <summary>
    /// The fill color for a node box, by its color category.
    /// </summary>
    public static MediaColor GetKindColor(string kind) => kind switch
    {
        "Parameter" => MediaColor.Parse("#3D7EBF"),
        "Grip" => MediaColor.Parse("#3D9E5F"),
        "Action" => MediaColor.Parse("#C77B3D"),
        "Component" => MediaColor.Parse("#7A7A7A"),
        _ => MediaColor.Parse("#8E6FBF"),
    };

    /// <summary>
    /// The accent brush the user picked in the settings flyout (a null theme
    /// would resolve the light dictionary — pass the actual variant explicitly).
    /// </summary>
    public static IBrush GetAccentBrush()
    {
        var app = Application.Current;
        if (app is not null)
        {
            if (app.FindResource(app.ActualThemeVariant, "AccentFillColorDefaultBrush") is IBrush brush)
            {
                return brush;
            }
        }

        return new SolidColorBrush(MediaColor.Parse("#0078D4"));
    }
}
