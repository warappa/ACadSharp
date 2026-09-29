using Avalonia.Media;

namespace ACadSharp.Viewer;

/// <summary>
/// Shadows as C# statics, referenced from XAML via
/// <c>BoxShadow="{x:Static local:DesignTokenShadows.CardShadow}"</c>.
/// BoxShadows has no TypeConverter in Avalonia 12, so a XAML resource form
/// is not possible (it ICEs); this is the verified pattern.
///
/// Each elevation is the design language's two-part recipe: a *cast* (large,
/// soft — the "floating") plus a *contact* (tight, just under the edge — the
/// "grounding"). The card is the base recipe; the tooltip (a higher
/// elevation) scales it up (larger and softer).
/// </summary>
public static class DesignTokenShadows
{
    // The card (elevation 8): the base two-part recipe — a cast
    // (0 10px 20px, 15% black) + a contact (0 3px 6px, 10% black). BoxShadows
    // is a struct whose two-item ctor takes (first, rest[]), so the cast is
    // the first and the contact is the single-element rest array.
    public static readonly BoxShadows CardShadow = new(
        new BoxShadow { OffsetY = 10, Blur = 20, Color = Avalonia.Media.Color.Parse("#26000000") },
        new[] { new BoxShadow { OffsetY = 3, Blur = 6, Color = Avalonia.Media.Color.Parse("#1A000000") } });

    // The tooltip (elevation 16, higher than the card): the recipe scaled up
    // (larger offset + blur, a touch more opaque) so it reads as the
    // topmost floating surface.
    public static readonly BoxShadows TooltipShadow = new(
        new BoxShadow { OffsetY = 16, Blur = 32, Color = Avalonia.Media.Color.Parse("#2E000000") },
        new[] { new BoxShadow { OffsetY = 5, Blur = 10, Color = Avalonia.Media.Color.Parse("#1F000000") } });
}
