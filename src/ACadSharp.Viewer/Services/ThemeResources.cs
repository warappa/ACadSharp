using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// Resolves the theme-dependent resources the viewer draws in code (the node
/// colors, the edge lines, the accent ring, the canvas / card backgrounds)
/// from the active theme, with a fixed fallback for each. The results are
/// cached and invalidated by <see cref="Refresh"/>, which the host calls when
/// the theme variant or the accent color changes — so a code-drawn brush is
/// resolved at most once per theme and stays correct across theme switches.
/// </summary>
public static class ThemeResources
{
    private static readonly Dictionary<string, IBrush> _brushes = new();
    private static readonly Dictionary<string, MediaColor> _colors = new();

    // FluentAvalonia theme resources (the accent and the canvas / card
    // backgrounds).

    /// <summary>
    /// The accent brush the user picked in the settings flyout (a null theme
    /// would resolve the light dictionary — the app's actual variant is passed
    /// explicitly below).
    /// </summary>
    public static IBrush Accent => Token("AccentFillColorDefaultBrush", "#0078D4");

    /// <summary>
    /// The graph canvas background (the window background color from the current
    /// theme — verified to match the dialog background in both themes). Used for
    /// the edge-label background mask.
    /// </summary>
    public static IBrush CanvasBackground => Token("SolidBackgroundFillColorBase", "#202020");

    /// <summary>
    /// The mini-map card background: the theme's elevated-surface color when it
    /// is defined, falling back to the base canvas color (verified to exist),
    /// then a fixed dark color.
    /// </summary>
    public static IBrush CardBackground => Token("SolidBackgroundFillColorSecondary", "#2D2D2D", "SolidBackgroundFillColorBase");

    // DesignTokens — the theme-dependent node-graph colors. The XAML legend
    // (NodeViewerDialog) and the code-drawn graph resolve the same keys, so
    // they agree across theme switches.

    /// <summary>
    /// The node box fill, by color category (the catch-all for other kinds).
    /// </summary>
    public static IBrush NodeKind(string kind) => kind switch
    {
        "Parameter" => Token("NodeParameterColor", "#3D7EBF"),
        "Grip" => Token("NodeGripColor", "#3D9E5F"),
        "Action" => Token("NodeActionColor", "#C77B3D"),
        "Component" => Token("NodeComponentColor", "#7A7A7A"),
        _ => Token("NodeOtherColor", "#8E6FBF"),
    };

    /// <summary>
    /// The target node's highlight ring (the legend's target swatch).
    /// </summary>
    public static IBrush NodeTargetStroke => Token("NodeTargetStrokeColor", "#FFFFFF");

    /// <summary>
    /// The node box text, on the fill: white on the Light theme's dark fills
    /// and black on the Dark theme's light fills (each passes 4.5:1 against
    /// every fill in its theme — see DesignTokens for the table).
    /// </summary>
    public static IBrush NodeText => Token("NodeTextColor", "#000000");
    public static IBrush NodeTextSecondary => Token("NodeTextSecondaryColor", "#CC000000");

    /// <summary>
    /// The hover ring, on the canvas: dark on the Light theme, light on the
    /// Dark theme (always readable against the canvas).
    /// </summary>
    public static IBrush NodeHover => Token("NodeHoverColor", "#F0F0F0");

    /// <summary>
    /// The port chrome (the label text on its canvas mask, the circle's
    /// fill): the canvas's own polarity — dark on Light, light on Dark.
    /// </summary>
    public static IBrush Port => Token("PortColor", "#FFFFFF");

    public static IBrush Feedback => Token("FeedbackColor", "#E8A33D");

    /// <summary>
    /// The edge line / label and the mini-map visible-area colors.
    /// </summary>
    public static IBrush EdgeLine => Token("EdgeLineColor", "#B09A9A9A");
    public static IBrush EdgeLabel => Token("EdgeLabelColor", "#E0808080");
    public static IBrush ViewRect => Token("ViewRectColor", "#30FFFFFF");

    /// <summary>
    /// The fixed (brand) status colors and the accent presets the settings
    /// flyout applies to the theme's CustomAccentColor.
    /// </summary>
    public static IBrush StatusError => Token("StatusErrorColor", "#D13438");
    public static IBrush StatusSuccess => Token("StatusSuccessColor", "#2E9E5B");
    public static MediaColor AccentBlue => Color("AccentChoiceBlue", "#0078D4");
    public static MediaColor AccentRed => Color("AccentChoiceRed", "#D13438");
    public static MediaColor AccentGreen => Color("AccentChoiceGreen", "#2E9E5B");
    public static MediaColor AccentPurple => Color("AccentChoicePurple", "#8764B8");

    /// <summary>
    /// Invalidates the cache; call when the theme variant or accent color
    /// changes so the next resolution picks up the new theme's brushes.
    /// </summary>
    public static void Refresh()
    {
        _brushes.Clear();
        _colors.Clear();
    }

    /// <summary>
    /// A token brush, cached. The token comes from the active theme (a
    /// DesignTokens theme-dependent key, or a FluentAvalana key); the fallback
    /// (the dark value) is used only when the token is missing.
    /// </summary>
    private static IBrush Token(string key, string fallback, string? fallbackKey = null)
    {
        if (_brushes.TryGetValue(key, out var hit))
        {
            return hit;
        }

        if (Application.Current is { } app)
        {
            // A null theme resolves against the light-theme dictionary, so pass
            // the app's actual theme variant explicitly.
            var resource = app.FindResource(app.ActualThemeVariant, key);
            if (resource is IBrush brush)
            {
                return _brushes[key] = brush;
            }
            if (resource is MediaColor color)
            {
                return _brushes[key] = new SolidColorBrush(color);
            }
            if (fallbackKey is not null)
            {
                var alt = app.FindResource(app.ActualThemeVariant, fallbackKey);
                if (alt is IBrush altBrush)
                {
                    return _brushes[key] = altBrush;
                }
                if (alt is MediaColor altColor)
                {
                    return _brushes[key] = new SolidColorBrush(altColor);
                }
            }
        }

        return _brushes[key] = new SolidColorBrush(MediaColor.Parse(fallback));
    }

    private static MediaColor Color(string key, string fallback)
    {
        if (_colors.TryGetValue(key, out var hit))
        {
            return hit;
        }

        if (Application.Current is { } app)
        {
            // A null theme resolves against the light-theme dictionary, so pass
            // the app's actual theme variant explicitly.
            var resource = app.FindResource(app.ActualThemeVariant, key);
            if (resource is MediaColor color)
            {
                return _colors[key] = color;
            }
        }

        return _colors[key] = MediaColor.Parse(fallback);
    }
}
