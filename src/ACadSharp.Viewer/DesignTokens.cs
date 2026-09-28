using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ACadSharp.Viewer;

/// <summary>
/// App design tokens (see DesignTokens.axaml). Avalonia 12: the type is
/// Avalonia.Controls.ResourceDictionary, loaded with
/// Avalonia.Markup.Xaml.AvaloniaXamlLoader.
/// </summary>
public partial class DesignTokens : ResourceDictionary
{
    public DesignTokens() => AvaloniaXamlLoader.Load(this);
}
