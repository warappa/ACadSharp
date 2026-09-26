using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// Resolves the theme-dependent brushes the viewer draws in code (the accent
/// ring, the graph edge-label mask, the mini-map card) from the active theme,
/// with a fixed fallback for each. The results are cached and invalidated by
/// <see cref="Refresh"/>, which the host calls when the theme variant or the
/// accent color changes — so a code-drawn brush is resolved at most once per
/// theme and stays correct across theme switches.
/// </summary>
public static class ThemeResources
{
    private static IBrush? _accent;
    private static IBrush? _canvasBackground;
    private static IBrush? _cardBackground;

    /// <summary>
    /// The accent brush the user picked in the settings flyout (a null theme
    /// would resolve the light dictionary — the app's actual variant is passed
    /// explicitly below).
    /// </summary>
    public static IBrush Accent => _accent ??= TryResolve("AccentFillColorDefaultBrush")
        ?? new SolidColorBrush(MediaColor.Parse("#0078D4"));

    /// <summary>
    /// The graph canvas background (the window background color from the current
    /// theme — verified to match the dialog background in both themes). Used for
    /// the edge-label background mask.
    /// </summary>
    public static IBrush CanvasBackground => _canvasBackground ??= TryResolve("SolidBackgroundFillColorBase")
        ?? new SolidColorBrush(MediaColor.Parse("#202020"));

    /// <summary>
    /// The mini-map card background: the theme's elevated-surface color when it
    /// is defined, falling back to the base canvas color (verified to exist),
    /// then a fixed dark color.
    /// </summary>
    public static IBrush CardBackground
    {
        get
        {
            if (_cardBackground is not null)
            {
                return _cardBackground;
            }

            return _cardBackground = TryResolve("SolidBackgroundFillColorSecondary")
                ?? TryResolve("SolidBackgroundFillColorBase")
                ?? new SolidColorBrush(MediaColor.Parse("#2D2D2D"));
        }
    }

    /// <summary>
    /// Invalidates the cache; call when the theme variant or accent color
    /// changes so the next resolution picks up the new theme's brushes.
    /// </summary>
    public static void Refresh()
    {
        _accent = null;
        _canvasBackground = null;
        _cardBackground = null;
    }

    private static IBrush? TryResolve(string key)
    {
        var app = Application.Current;
        if (app is null)
        {
            return null;
        }

        // A null theme resolves against the light-theme dictionary, so pass
        // the app's actual theme variant explicitly.
        var resource = app.FindResource(app.ActualThemeVariant, key);
        if (resource is IBrush brush)
        {
            return brush;
        }

        if (resource is MediaColor color)
        {
            return new SolidColorBrush(color);
        }

        return null;
    }
}
