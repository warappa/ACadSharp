using ACadSharp;
using ACadSharp.Viewer.Controls;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MediaColor = Avalonia.Media.Color;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

namespace ACadSharp.Viewer;

public partial class MainWindow : Window
{
    private enum StatusKind { Neutral, Success, Error }

    private static readonly MediaColor ErrorColor = MediaColor.Parse("#D13438");
    private static readonly MediaColor SuccessColor = MediaColor.Parse("#2E9E5B");

    private CadDocument? _document;
    private BlockTreeNode? _selectedNode;
    private bool _isBusy;
    private Timer? _infoBarAutoClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        string? path = await PickFileAsync();
        if (path == null)
        {
            return;
        }

        await LoadFileAsync(path);
    }

    private async Task LoadFileAsync(string path)
    {
        _isBusy = true;
        LoadingRing.IsVisible = true;
        FileNameText.Text = path;
        SetStatus($"Loading {Path.GetFileName(path)}…", StatusKind.Neutral);

        try
        {
            // Read + parse off the UI thread so the window stays responsive.
            CadDocument document = await Task.Run(() => CadFileService.LoadFile(path));
            _document = document;

            var tree = BlockTreeModel.Build(document);
            BlockTree.ItemsSource = tree;
            BlockHeader.Text = string.Empty;
            EvalStatus.Text = string.Empty;
            PropertyGrid.ItemsSource = null;
            PlaceholderText.Text = "Select a block in the tree to see its properties.";
            PlaceholderText.IsVisible = true;

            SetStatus($"Loaded {Path.GetFileName(path)}: {document.BlockRecords.Count} block record(s), {tree.Count} root(s).", StatusKind.Success);
            ShowInfoBar(
                FluentAvalonia.UI.Controls.FAInfoBarSeverity.Success,
                "File loaded",
                $"{Path.GetFileName(path)} — {document.BlockRecords.Count} block record(s).");
        }
        catch (Exception ex)
        {
            _document = null;
            SetStatus($"Failed to load {Path.GetFileName(path)}: {ex.Message}", StatusKind.Error);
            ShowInfoBar(
                FluentAvalonia.UI.Controls.FAInfoBarSeverity.Error,
                "Load failed",
                ex.Message);
        }
        finally
        {
            _isBusy = false;
            LoadingRing.IsVisible = false;
        }
    }

    private async Task<string?> PickFileAsync()
    {
        if (this.StorageProvider is null)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Open DWG/DXF file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CAD files") { Patterns = new[] { "*.dwg", "*.dxf" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*.*" } },
            },
        };

        IReadOnlyList<IStorageFile> files = await this.StorageProvider.OpenFilePickerAsync(options);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (BlockTree.SelectedItem is not BlockTreeNode node)
        {
            return;
        }

        _selectedNode = node;
        BlockHeader.Text = node.Block.Name + (node.Block.IsDynamic ? " (dynamic)" : string.Empty);

        BlockModel? model = BlockModel.Create(node.Block);
        if (model is null)
        {
            EvalStatus.Text = "Not a dynamic block (no evaluation graph).";
            PropertyGrid.ItemsSource = null;
            PlaceholderText.Text = "This block has no parameters.";
            PlaceholderText.IsVisible = true;
            return;
        }

        EvalStatus.Text = model.EvaluationOk
            ? $"Evaluation OK ({model.GripCount} grip(s) activated)"
            : "Evaluation FAILED — values may be incomplete.";

        PropertyGrid.ItemsSource = model.Properties;
        PlaceholderText.Text = "No parameters in this block.";
        PlaceholderText.IsVisible = model.Properties.Count == 0;
    }

    private void OnInfoClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PropertyItem item } || _selectedNode is null)
        {
            return;
        }

        if (_selectedNode.Block.EvaluationGraph is not { } graph)
        {
            return;
        }

        var dialog = new NodeViewerDialog(_selectedNode.Block, item, graph);
        dialog.ShowDialog(this);
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = DarkThemeToggle.IsChecked == true
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText.Text = text;
        StatusText.Foreground = kind switch
        {
            StatusKind.Error => new SolidColorBrush(ErrorColor),
            StatusKind.Success => new SolidColorBrush(SuccessColor),
            _ => null, // revert to the styled (tertiary) default
        };
    }

    private void ShowInfoBar(FluentAvalonia.UI.Controls.FAInfoBarSeverity severity, string title, string message)
    {
        _infoBarAutoClose?.Dispose();
        _infoBarAutoClose = null;

        InfoBar.Severity = severity;
        InfoBar.Title = title;
        InfoBar.Message = message;
        InfoBar.IsOpen = true;

        // Success/info bars auto-dismiss; errors stay until dismissed.
        if (severity == FluentAvalonia.UI.Controls.FAInfoBarSeverity.Success
            || severity == FluentAvalonia.UI.Controls.FAInfoBarSeverity.Informational)
        {
            _infoBarAutoClose = new Timer(4000) { AutoReset = false };
            _infoBarAutoClose.Elapsed += (_, _) =>
            {
                try
                {
                    _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => InfoBar.IsOpen = false);
                }
                catch (InvalidOperationException)
                {
                    // App is shutting down; nothing to do.
                }
            };
            _infoBarAutoClose.Start();
        }
    }
}
