using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;

namespace ACadSharp.Viewer.Controls;

/// <summary>
/// Modeless dialog listing a document's block properties tables (the
/// "display table" UI feature of a dynamic block's properties set) with their
/// decoded data: the header fields (be_major / be_minor / eed1071), the
/// schema-dependent records (1kV: one 7-bit string-pool index per record;
/// L3-02: a label + value pair) resolved against the interned string pool,
/// and the pool itself.
/// </summary>
public partial class BlockPropertiesTableDialog : Window
{
    public BlockPropertiesTableDialog(List<BptTableItem> tables)
    {
        InitializeComponent();

        if (MainWindow.UseSystemTitleBar)
        {
            // The same dev switch as the main window (ACADSHARP_VIEWER_TITLEBAR=system):
            // the child strip goes away and the caption is the OS's again.
            ExtendClientAreaToDecorationsHint = false;
            ChildTitleStrip.IsVisible = false;
        }

        Header.Text = $"Block properties tables ({tables.Count})";

        // The child titlebar shows the window title, so it carries the same
        // identity the header does; the WM title matches too (tools/x11-chrome-probe).
        Title = $"Block properties tables ({tables.Count})";

        TableList.ItemsSource = tables;
        TableList.SelectionChanged += OnTableSelected;
        TableList.SelectedIndex = 0;
        // Applying the first item directly (as well as through the selection
        // event) so the details are populated even before the ListBox has
        // realized its first container.
        if (TableList.SelectedItem is BptTableItem first)
        {
            Apply(first);
        }
    }

    private void OnTableSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (TableList.SelectedItem is BptTableItem item)
        {
            Apply(item);
        }
    }

    private void Apply(BptTableItem item)
    {
        BeMajorValue.Text = item.BeMajorText;
        BeMinorValue.Text = item.BeMinorText;
        Eed1071Value.Text = item.Eed1071Text;
        RawBitsValue.Text = item.RawBitsText;
        PoolStartValue.Text = item.PoolStartText;
        StringCountValue.Text = $"{item.StringCount}";
        SchemaTag.Text = item.SchemaText;
        RecordsNote.Text = item.RecordsNote;
        RecordsGrid.ItemsSource = item.Records;
        PoolHeader.Text = $"String pool ({item.StringCount})";
        PoolGrid.ItemsSource = item.Strings;
    }

    // --- App-drawn child titlebar (FluentStyles.axaml, ChildWindowChrome.cs) ---

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ChildWindowChrome.BeginDrag(this, e);
    }

    private void OnCaptionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ChildWindowChrome.ConsumeCaptionPress(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
