using ACadSharp;
using ACadSharp.Viewer.Controls;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using MediaColor = Avalonia.Media.Color;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
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
    private string? _lastPath;

    public MainWindow()
    {
        InitializeComponent();

        // Window icon — Avalonia 12's Window.Icon is WindowIcon? (not IImage?), and the
        // string ctor routes through the platform icon loader, so load the embedded
        // asset as a stream (deterministic on all platforms).
        using var iconStream = AssetLoader.Open(new Uri("avares://ACadSharp.Viewer/Assets/icon.png", UriKind.Absolute));
        Icon = new WindowIcon(iconStream);

        // Drag & drop — Avalonia 12 removed AllowDrop/DragOver/Drop from TopLevel;
        // the API is now the static DragDrop class (attached property + attached events).
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        _ = OpenFileAsync();
    }

    private async void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        _ = ReloadAsync();
    }

    private async Task OpenFileAsync()
    {
        if (_isBusy)
        {
            return;
        }

        string? path = await PickFileAsync();
        if (path != null)
        {
            await LoadFileAsync(path);
        }
    }

    private async Task ReloadAsync()
    {
        if (_isBusy || _lastPath is null)
        {
            return;
        }

        await LoadFileAsync(_lastPath);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.O)
        {
            _ = OpenFileAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            _ = ReloadAsync();
            e.Handled = true;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        IStorageItem? item = e.DataTransfer.TryGetFile();
        string? path = item?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not (".dwg" or ".dxf"))
        {
            SetStatus($"Unsupported file type: {Path.GetFileName(path)} (expected .dwg or .dxf)", StatusKind.Error);
            return;
        }

        await LoadFileAsync(path);
    }

    private async Task LoadFileAsync(string path)
    {
        _isBusy = true;
        _lastPath = path;
        ReloadButton.IsEnabled = true;
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
                FAInfoBarSeverity.Success,
                "File loaded",
                $"{Path.GetFileName(path)} — {document.BlockRecords.Count} block record(s).");
        }
        catch (Exception ex)
        {
            _document = null;
            SetStatus($"Failed to load {Path.GetFileName(path)}: {ex.Message}", StatusKind.Error);
            ShowInfoBar(
                FAInfoBarSeverity.Error,
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
        bool isDark = DarkThemeToggle.IsChecked == true;
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        ThemeIcon.Symbol = isDark ? FASymbol.DarkTheme : FASymbol.WeatherSunny;
        ThemeLabel.Text = isDark ? "Dark" : "Light";
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

    private void ShowInfoBar(FAInfoBarSeverity severity, string title, string message)
    {
        _infoBarAutoClose?.Dispose();
        _infoBarAutoClose = null;

        InfoBar.Severity = severity;
        InfoBar.Title = title;
        InfoBar.Message = message;
        InfoBar.IsOpen = true;

        // Success/info bars auto-dismiss; errors stay until dismissed.
        if (severity == FAInfoBarSeverity.Success
            || severity == FAInfoBarSeverity.Informational)
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
