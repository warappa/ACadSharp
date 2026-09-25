using ACadSharp;
using ACadSharp.Viewer.Controls;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ACadSharp.Viewer;

public partial class MainWindow : Window
{
    private CadDocument? _document;
    private BlockTreeNode? _selectedNode;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        string? path = await PickFileAsync();
        if (path == null)
        {
            return;
        }

        FileNameText.Text = path;
        StatusText.Text = $"Loading {Path.GetFileName(path)}…";

        try
        {
            _document = CadFileService.LoadFile(path);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to load: {ex.Message}";
            return;
        }

        var tree = BlockTreeModel.Build(_document);
        BlockTree.ItemsSource = tree;
        BlockHeader.Text = string.Empty;
        EvalStatus.Text = string.Empty;
        PropertyGrid.ItemsSource = null;
        PlaceholderText.Text = "Select a block in the tree to see its properties.";
        PlaceholderText.IsVisible = true;
        StatusText.Text = $"Loaded {Path.GetFileName(path)}: {_document.BlockRecords.Count} block record(s), {tree.Count} root(s).";
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
}
