using ACadSharp;
using ACadSharp.Tables;
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
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace ACadSharp.Viewer;

public partial class MainWindow : Window
{
    private enum StatusKind { Neutral, Success, Error }

    private static readonly MediaColor ErrorColor = MediaColor.Parse("#D13438");
    private static readonly MediaColor SuccessColor = MediaColor.Parse("#2E9E5B");

    // FluentAvalonia 3.x accent presets — applied to the theme instance's
    // CustomAccentColor (3.x has no ApplicationAccentColorManager).
    private static readonly MediaColor AccentBlue = MediaColor.Parse("#0078D4");
    private static readonly MediaColor AccentRed = MediaColor.Parse("#D13438");
    private static readonly MediaColor AccentGreen = MediaColor.Parse("#2E9E5B");
    private static readonly MediaColor AccentPurple = MediaColor.Parse("#8764B8");

    // First-run flag for the teaching tip (user profile, not the repo).
    private static string FirstRunFlagPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ACadSharp.Viewer",
            "intro-tip-seen");

    private CadDocument? _document;
    private BlockTreeNode? _selectedNode;
    private List<PropertyItem>? _properties;
    private bool _isBusy;
    private Timer? _infoBarAutoClose;
    private string? _lastPath;
    private List<BlockTreeNode> _fullTree = new();
    private FAMenuFlyout? _settingsFlyout;

    // Per-block evaluation models for the current document. BlockModel.Create
    // activates and evaluates the shared EvaluationGraph (a side effect), so
    // re-running it on every selection of the same block is wasted work; this
    // cache makes the first evaluation sticky and is cleared when a new
    // document is loaded.
    private readonly Dictionary<BlockRecord, BlockModel?> _blockModelCache = new();

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

        // First-run teaching tip: anchored to the property grid (where the
        // info buttons live), shown once, then a flag is persisted in the
        // user profile so it does not nag again.
        IntroTip.Target = PropertyGrid;
        if (!HasSeenIntroTip)
        {
            IntroTip.IsOpen = true;
            MarkIntroTipSeen();
        }

        // Avalonia 12: FAMenuFlyout (FlyoutBase) has no Name property, so the
        // flyout cannot be x:Named; grab the instance from the button instead.
        _settingsFlyout = SettingsButton.Flyout as FAMenuFlyout;
    }

    private void OnTreeSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyTreeFilter();
    }

    private void ApplyTreeFilter()
    {
        string query = (TreeSearchBox.Text ?? string.Empty).Trim();

        List<BlockTreeNode> visible = query.Length == 0
            ? _fullTree
            : _fullTree
                .Select(n => FilterNode(n, query))
                .Where(n => n is not null)
                .Cast<BlockTreeNode>()
                .ToList();

        BlockTree.ItemsSource = visible;
        TreeEmptyText.Text = visible.Count == 0
            ? (query.Length == 0 ? "No file loaded — open a .dwg or .dxf (Ctrl+O)" : "No blocks match")
            : string.Empty;
        TreeEmptyText.IsVisible = visible.Count == 0;
    }

    /// <summary>
    /// Returns a copy of the node if the node or any descendant matches the
    /// query: a matching node keeps all its children (full context); a
    /// non-matching node keeps only the matching children (breadcrumb path).
    /// </summary>
    private static BlockTreeNode? FilterNode(BlockTreeNode node, string query)
    {
        bool selfMatches = node.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);

        List<BlockTreeNode> matchingChildren = node.Children
            .Select(c => FilterNode(c, query))
            .Where(c => c is not null)
            .Cast<BlockTreeNode>()
            .ToList();

        if (!selfMatches && matchingChildren.Count == 0)
        {
            return null;
        }

        return new BlockTreeNode(node.Block, node.DisplayName, selfMatches ? node.Children : matchingChildren);
    }

    private void OnExpandAllClick(object? sender, RoutedEventArgs e)
    {
        SetAllExpanded(true);
    }

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e)
    {
        SetAllExpanded(false);
    }

    private void SetAllExpanded(bool expanded)
    {
        // Avalonia 12's TreeView has no ExpandAll/CollapseAll; it is not
        // virtualized, so every item's container is realized and reachable.
        foreach (Control c in BlockTree.GetRealizedTreeContainers())
        {
            if (c is TreeViewItem item)
            {
                item.IsExpanded = expanded;
            }
        }
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

    public async Task LoadFileAsync(string path)
    {
        _isBusy = true;
        _blockModelCache.Clear();
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

            _fullTree = BlockTreeModel.Build(document);
            TreeSearchBox.Text = string.Empty;
            ApplyTreeFilter();

            int total = document.BlockRecords.Count;
            int dynamic = document.BlockRecords.Count(b => b.IsDynamic);
            TreeSummary.Text = $"{total} block(s): {dynamic} dynamic, {total - dynamic} static";

            BlockHeader.Text = string.Empty;
            EvalStatus.Text = string.Empty;
            _properties = null;
            PropertyGrid.ItemsSource = null;
            PlaceholderText.Text = "Select a block in the tree to see its properties.";
            PlaceholderText.IsVisible = true;

            SetStatus($"Loaded {Path.GetFileName(path)}: {total} block(s), {_fullTree.Count} root(s).", StatusKind.Success);
            ShowInfoBar(
                FAInfoBarSeverity.Success,
                "File loaded",
                $"{Path.GetFileName(path)} — {total} block(s).");
        }
        catch (Exception ex)
        {
            _document = null;
            _fullTree = new List<BlockTreeNode>();
            TreeSummary.Text = string.Empty;
            ApplyTreeFilter();
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

    /// <summary>
    /// Returns the evaluation model for a block, building it on first use and
    /// caching it so the shared EvaluationGraph is evaluated at most once per
    /// block per document (re-selecting a block is then free).
    /// </summary>
    private BlockModel? GetBlockModel(BlockRecord block)
    {
        if (_blockModelCache.TryGetValue(block, out BlockModel? cached))
        {
            return cached;
        }

        BlockModel? model = BlockModel.Create(block);
        _blockModelCache[block] = model;
        return model;
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

        BlockModel? model = GetBlockModel(node.Block);
        if (model is null)
        {
            EvalStatus.Text = "Not a dynamic block (no evaluation graph).";
            _properties = null;
            PropertyGrid.ItemsSource = null;
            PlaceholderText.Text = "This block has no parameters.";
            PlaceholderText.IsVisible = true;
            return;
        }

        EvalStatus.Text = model.EvaluationOk
            ? $"Evaluation OK ({model.GripCount} grip(s) activated)"
            : "Evaluation FAILED — values may be incomplete.";

        _properties = model.Properties;
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

        OpenNodeViewer(item);
    }

    /// <summary>
    /// Opens the node viewer (modeless) for the given property.
    /// </summary>
    public void OpenNodeViewer(PropertyItem item)
    {
        if (_selectedNode is null || _selectedNode.Block.EvaluationGraph is not { } graph)
        {
            return;
        }

        new NodeViewerDialog(_selectedNode.Block, item, graph).ShowDialog(this);
    }

    /// <summary>
    /// Verification helper (used by the --screenshot mode): expands the
    /// ancestors of the first dynamic block in the tree and selects it, so
    /// its properties show in the grid. Returns the selected node, or null
    /// when no dynamic block was found.
    /// </summary>
    public BlockTreeNode? SelectFirstDynamicNode()
    {
        List<BlockTreeNode> path = new();
        if (!FindFirstDynamic(_fullTree, path))
        {
            return null;
        }

        // Expand the ancestors so the target node is visible.
        foreach (BlockTreeNode ancestor in path)
        {
            foreach (Control c in BlockTree.GetRealizedTreeContainers())
            {
                if (c is TreeViewItem tvi && ReferenceEquals(tvi.DataContext, ancestor))
                {
                    tvi.IsExpanded = true;
                }
            }
        }

        BlockTreeNode target = path[^1];
        BlockTree.SelectedItem = target;
        return target;
    }

    /// <summary>
    /// Verification helper (used by the --screenshot mode): selects the
    /// first dynamic block in the tree and opens its node viewer for the
    /// first property, so the dialog can be captured headlessly. Returns
    /// the dialog window, or null when no dynamic block was found.
    /// </summary>
    public TopLevel? OpenFirstDynamicNodeViewer()
    {
        BlockTreeNode? target = SelectFirstDynamicNode();
        if (target is null)
        {
            return null;
        }

        BlockModel? model = GetBlockModel(target.Block);
        if (model is null || model.Properties.Count == 0 || target.Block.EvaluationGraph is not { } graph)
        {
            return null;
        }

        var dialog = new NodeViewerDialog(target.Block, model.Properties[0], graph);
        dialog.Show();
        return dialog;
    }

    private static bool FindFirstDynamic(List<BlockTreeNode> nodes, List<BlockTreeNode> path)
    {
        foreach (BlockTreeNode node in nodes)
        {
            if (node.IsDynamic)
            {
                path.Add(node);
                return true;
            }

            if (FindFirstDynamic(node.Children, path))
            {
                path.Insert(0, node);
                return true;
            }
        }

        return false;
    }

    private void OnThemeToggleClick(object? sender, RoutedEventArgs e)
    {
        bool isDark = DarkThemeToggle.IsChecked == true;
        ApplyTheme(isDark ? ThemeVariant.Dark : ThemeVariant.Light);
    }

    private void ApplyTheme(ThemeVariant variant)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = variant;
        }

        // Sync the header toggle + the settings-flyout radio items.
        bool isDark = variant == ThemeVariant.Dark;
        bool isHc = variant == FluentAvaloniaTheme.HighContrastTheme;
        ThemeIcon.Symbol = isDark ? FASymbol.DarkTheme : isHc ? FASymbol.Highlight : FASymbol.WeatherSunny;
        ThemeLabel.Text = isDark ? "Dark" : isHc ? "High contrast" : "Light";
        DarkThemeToggle.IsChecked = isDark;
        ThemeLightItem.IsChecked = !isDark && !isHc;
        ThemeDarkItem.IsChecked = isDark;
        ThemeHcItem.IsChecked = isHc;

        // The value-foreground converter resolves theme brushes once per bind;
        // re-binding forces it to pick up the new theme's brushes.
        if (_properties is not null)
        {
            PropertyGrid.ItemsSource = null;
            PropertyGrid.ItemsSource = _properties;
        }
    }

    private void OnSettingsThemeClick(object? sender, RoutedEventArgs e)
    {
        ThemeVariant? variant = (sender as FAMenuFlyoutItem)?.CommandParameter switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            "HighContrast" => FluentAvaloniaTheme.HighContrastTheme,
            _ => null,
        };
        if (variant is not null)
        {
            ApplyTheme(variant);
        }
    }

    private void OnSettingsAccentClick(object? sender, RoutedEventArgs e)
    {
        MediaColor? accent = (sender as FAMenuFlyoutItem)?.CommandParameter switch
        {
            "AccentDefault" => null,
            "AccentBlue" => AccentBlue,
            "AccentRed" => AccentRed,
            "AccentGreen" => AccentGreen,
            "AccentPurple" => AccentPurple,
            _ => null,
        };

        // 3.x: the accent is set on the theme instance (6 variants are
        // pregenerated); null restores the system/default accent.
        if (App.Theme is not null)
        {
            App.Theme.CustomAccentColor = accent;
        }
    }

    private static bool HasSeenIntroTip
    {
        get
        {
            try
            {
                return File.Exists(FirstRunFlagPath);
            }
            catch
            {
                return true; // cannot check; do not nag
            }
        }
    }

    private static void MarkIntroTipSeen()
    {
        try
        {
            string dir = Path.GetDirectoryName(FirstRunFlagPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FirstRunFlagPath, DateTime.Now.ToString("O"));
        }
        catch
        {
            // Non-fatal: the tip just shows again next time.
        }
    }

    /// <summary>
    /// Verification helper (used by the --screenshot mode): shows the
    /// first-run teaching tip regardless of the first-run flag.
    /// </summary>
    public void ForceShowIntroTip()
    {
        IntroTip.IsOpen = true;
    }

    /// <summary>
    /// Verification helper (used by the --screenshot mode): opens the
    /// settings flyout so it can be captured headlessly.
    /// </summary>
    public void ForceOpenSettingsFlyout()
    {
        _settingsFlyout?.IsOpen = true;
    }

    /// <summary>
    /// Verification helper (used by the --screenshot mode): applies a theme
    /// variant and/or an accent color (the same operations the settings
    /// flyout performs), so the results can be captured headlessly.
    /// </summary>
    public void ApplyThemeAndAccentForVerification(ThemeVariant? variant, MediaColor? accent)
    {
        if (variant is not null)
        {
            ApplyTheme(variant);
        }

        if (App.Theme is not null)
        {
            App.Theme.CustomAccentColor = accent;
        }
    }

    private void OnIntroTipActionClick(FATeachingTip sender, EventArgs e)
    {
        IntroTip.IsOpen = false;
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
