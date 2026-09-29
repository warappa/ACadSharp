using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ACadSharp.Viewer;

/// <summary>
/// App design tokens (see DesignTokens.axaml). Avalonia 12: the type is
/// Avalonia.Controls.ResourceDictionary, loaded with
/// Avalonia.Markup.Xaml.AvaloniaXamlLoader.
/// </summary>
public partial class DesignTokens : ResourceDictionary
{
    public DesignTokens() => AvaloniaXamlLoader.Load(this);

    // C# accessors for the theme-invariant tokens. The code-drawn graph reads
    // the same values the XAML uses — resolved from the app's resources (the
    // dictionary is merged into Application.Resources) with a fixed fallback
    // so it also works before the app is set up (e.g. in unit tests).
    //
    // The theme-DEPENDENT colors are intentionally NOT accessed here; they go
    // through ThemeResources (which resolves them for the active theme).

    public static double CaptionFontSize => Get("CaptionFontSize", 11.0);
    public static double BodyFontSize => Get("BodyFontSize", 14.0);
    public static double SubtitleFontSize => Get("SubtitleFontSize", 16.0);
    public static double CodeFontSize => Get("CodeFontSize", 12.0);
    public static FontFamily FontFamilyCode => Get("FontFamilyCode", new FontFamily("Cascadia Code, Consolas, monospace"));
    public static CornerRadius CardCornerRadius => Get("CardCornerRadius", new CornerRadius(6));
    public static CornerRadius OverlayCornerRadius => Get("OverlayCornerRadius", new CornerRadius(8));
    public static CornerRadius SwatchCornerRadius => Get("SwatchCornerRadius", new CornerRadius(3));

    private static T Get<T>(string key, T fallback)
    {
        var app = Application.Current;
        if (app is not null)
        {
            // The static tokens live at the dictionary root (theme-invariant),
            // so a null theme looks at the root entries.
            var resource = app.FindResource(null, key);
            if (resource is T value)
            {
                return value;
            }
        }

        return fallback;
    }
}
