using Avalonia.Media;

namespace ACadSharp.Viewer;

/// <summary>
/// Shadows as C# statics, referenced from XAML via
/// <c>BoxShadow="{x:Static local:DesignTokenShadows.CardShadow}"</c>.
/// BoxShadows has no TypeConverter in Avalonia 12, so a XAML resource form
/// is not possible (it ICEs); this is the verified pattern.
/// </summary>
public static class DesignTokenShadows
{
    public static readonly BoxShadows CardShadow = new(new BoxShadow
    {
        OffsetY = 8,
        Blur = 16,
        Spread = 0,
        Color = Avalonia.Media.Color.Parse("#2E000000"),
    });

    public static readonly BoxShadows TooltipShadow = new(new BoxShadow
    {
        OffsetY = 16,
        Blur = 24,
        Spread = 0,
        Color = Avalonia.Media.Color.Parse("#4D000000"),
    });
}
