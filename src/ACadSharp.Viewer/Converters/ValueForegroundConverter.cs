using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MediaColor = Avalonia.Media.Color;

namespace ACadSharp.Viewer.Converters;

/// <summary>
/// Foreground for a property value cell: secondary when a value is set,
/// tertiary (grayed) when unset. Resolved from the active theme so light/dark
/// switching keeps working (the host re-binds the grid on theme change).
/// </summary>
public class ValueForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is bool b && b;
        var key = hasValue ? "TextFillColorSecondary" : "TextFillColorTertiary";
        // A null theme resolves against the light-theme dictionary, so pass
        // the app's actual theme variant explicitly.
        var app = Application.Current;
        var resource = app?.FindResource(app.ActualThemeVariant, key);
        if (resource is IBrush brush)
        {
            return brush;
        }
        if (resource is MediaColor color)
        {
            return new SolidColorBrush(color);
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
