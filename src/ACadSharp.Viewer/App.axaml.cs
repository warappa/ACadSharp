using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FluentAvalonia.Styling;
using System.Linq;

namespace ACadSharp.Viewer;

public partial class App : Application
{
    /// <summary>
    /// The FluentAvalonia theme instance. FluentAvalonia 3.x has no
    /// ApplicationAccentColorManager / ApplicationThemeManager; the accent
    /// color is set on the theme instance itself (CustomAccentColor).
    /// </summary>
    public static FluentAvaloniaTheme? Theme { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Theme = this.Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}