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
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ACadSharp.Viewer;

public partial class MainWindow : Window
{
    private enum StatusKind { Neutral, Success, Error }

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
    private string? _lastPath;
    private List<BlockTreeNode> _fullTree = new();
    private FAMenuFlyout? _settingsFlyout;

    // The open (modeless) node viewer, if any: the settings flyout stays
    // reachable while it is open, so a theme/accent change must refresh it.
    // (The normal Info-button path opens a modal dialog, which blocks this
    // window, so the theme cannot change while it is open and needs no hook.)
    private NodeViewerDialog? _nodeViewerDialog;

    // The open (modeless) block properties tables dialog, if any: one dialog
    // serves the whole document, so a second click activates the existing one
    // instead of stacking another.
    private BlockPropertiesTableDialog? _bptDialog;

    // Per-block evaluation models for the current document. BlockModel.Create
    // activates and evaluates the shared EvaluationGraph (a side effect), so
    // re-running it on every selection of the same block is wasted work; this
    // cache makes the first evaluation sticky and is cleared when a new
    // document is loaded.
    private readonly Dictionary<BlockRecord, BlockModel?> _blockModelCache = new();

    // --- Titlebar ownership -------------------------------------------------
    // The app owns the caption bar on every platform: the strip in MainWindow.axaml
    // is the titlebar (glyph, title, file name, Fluent caption buttons) and
    // ExtendClientAreaToDecorationsHint pulls the client area up into the frame.
    //
    // Windows and macOS do client-side decorations natively, so that hint is all
    // that is needed. Linux runs on the X11 backend — inside a Wayland session too,
    // via XWayland (the native Avalonia.Wayland backend is not referenced) — where
    // window managers decorate the frame server-side and stacked their own caption
    // on top of the strip. Avalonia 12 added the companion switch for exactly this:
    // X11PlatformOptions.EnableDrawnDecorations (Program.cs) makes Avalonia draw the
    // border, shadow and resize grips and stops it from asking the WM for a frame,
    // so the app's strip is the only titlebar.
    //
    // Non-client input is routed by role (Avalonia 12's WindowDecorationProperties.
    // ElementRole): the strip declares TitleBar, the caption buttons declare
    // Minimize/Maximize/Close, so a press on a button is a button press and a press
    // on the strip is a window move. OnTitleBarPointerPressed keeps the same
    // behaviour as a fallback for backends without role routing (the headless
    // renderer used by --screenshot).
    //
    // ACADSHARP_VIEWER_TITLEBAR=system hands the bar back to the OS (hides the strip,
    // the file name moves into the window Title); =custom forces the app-drawn one.
    private static bool UseSystemTitleBar
    {
        get
        {
            string? forced = Environment.GetEnvironmentVariable("ACADSHARP_VIEWER_TITLEBAR");
            if (string.Equals(forced, "system", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(forced, "custom", StringComparison.OrdinalIgnoreCase))
                return false;
            return false;
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        if (UseSystemTitleBar)
        {
            // Let the WM/compositor own the caption: no client-area extension,
            // no app-drawn strip, no app caption buttons.
            ExtendClientAreaToDecorationsHint = false;
            AppTitleStrip.IsVisible = false;
        }

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

        // The maximize glyph tracks the window state, which the window manager (or
        // a keyboard/tiling action) can change without going through our button.
        // Avalonia 12 has no Window.StateChanged event, so watch the property.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                UpdateMaximizeGlyph();
            }
        };

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
        if (visible.Count == 0)
        {
            bool noFile = query.Length == 0;
            SetEmptyState(
                TreeEmptyIcon, TreeEmptyTitle, TreeEmptyDesc, TreeEmptyPanel,
                noFile ? FASymbol.OpenFile : FASymbol.Document,
                noFile ? "No file loaded" : "No blocks match",
                noFile
                    ? "Open a .dwg or .dxf (Ctrl+O) to explore its dynamic blocks and evaluation graphs."
                    : $"No blocks match \"{query}\".",
                visible: true);
        }
        else
        {
            TreeEmptyPanel.IsVisible = false;
        }
    }

    /// <summary>
    /// Shows a standard empty state (icon + title + description) for the tree or
    /// property grid: sets the icon glyph, the title and description text, and
    /// toggles the container's visibility.
    /// </summary>
    private static void SetEmptyState(
        FASymbolIcon icon, TextBlock title, TextBlock desc, Control panel,
        FASymbol symbol, string titleText, string descText, bool visible)
    {
        icon.Symbol = symbol;
        title.Text = titleText;
        desc.Text = descText;
        panel.IsVisible = visible;
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

        return new BlockTreeNode(node.Block, node.ReferenceCount, node.IsCycleRoot, selfMatches ? node.Children : matchingChildren);
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
        await OpenFileAsync();
    }

    private async void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        await ReloadAsync();
    }

    // --- Custom titlebar (app-drawn caption) ---------------------------------
    // The strip declares ElementRole=TitleBar, so Avalonia routes a press on it to a
    // window move; the caption buttons declare their own roles and get their clicks.
    // OnTitleBarPointerPressed below is the same behaviour for the backends that do
    // not route by role (the headless renderer used by --screenshot).
    //
    // Avalonia 12 dropped the Tapped/DoubleTapped events, so the caption's
    // double-click (maximize/restore) is detected by pairing the presses here.
    // Windows and macOS do it themselves; X11 client-side decorations do not
    // (avalonia#22239).
    private const ulong TitleBarDoubleClickUs = 500_000;
    private const double TitleBarDoubleClickRadius = 8;

    private ulong _titleBarLastPressUs;
    private Point _titleBarLastPressPoint;

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point point = e.GetPosition(this);
        ulong us = e.Timestamp;

        bool isDoubleClick = _titleBarLastPressUs != 0
            && us - _titleBarLastPressUs <= TitleBarDoubleClickUs
            && Math.Abs(point.X - _titleBarLastPressPoint.X) <= TitleBarDoubleClickRadius
            && Math.Abs(point.Y - _titleBarLastPressPoint.Y) <= TitleBarDoubleClickRadius;

        _titleBarLastPressUs = us;
        _titleBarLastPressPoint = point;

        if (isDoubleClick
            && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            UpdateMaximizeGlyph();
            return;
        }

        BeginMoveDrag(e);
    }

    private void OnCaptionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true; // stop the titlebar drag from starting
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeGlyph();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeIcon is FASymbolIcon icon)
        {
            icon.Symbol = WindowState == WindowState.Maximized ? FASymbol.Restore : FASymbol.FullScreenMaximize;
        }
    }

    private async Task OpenFileAsync()
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            // LoadFileAsync handles its own load errors; this only covers the
            // file-dialog step (PickFileAsync), which the platform can throw.
            string? path = await PickFileAsync();
            if (path != null)
            {
                await LoadFileAsync(path);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to open a file: {ex.Message}", StatusKind.Error);
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

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.O)
        {
            await OpenFileAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            await ReloadAsync();
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
        // A still-open tables dialog would keep the previous file's data.
        if (_bptDialog is { IsVisible: true })
        {
            _bptDialog.Close();
        }
        ReloadButton.IsEnabled = true;
        TablesButton.IsEnabled = true;
        LoadingRing.IsVisible = true;
        // Win11 caption convention: the strip carries the file *name*; the full path
        // goes to its tooltip. (The old system-titlebar mode is gone by default, and
        // the window Title carries the same name for the taskbar / WM menu.)
        FileNameText.Text = Path.GetFileName(path);
        ToolTip.SetTip(FileNameText, path);
        Title = $"ACadSharp Viewer — {Path.GetFileName(path)}";
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
            SetEmptyState(
                PlaceholderIcon, PlaceholderTitle, PlaceholderDesc, PlaceholderPanel,
                FASymbol.OpenFolder,
                "No block selected",
                "Select a block in the tree to see its properties and evaluation values.",
                visible: true);

            SetStatus($"Loaded {Path.GetFileName(path)}: {total} block(s), {_fullTree.Count} root(s).", StatusKind.Success);
        }
        catch (Exception ex)
        {
            _document = null;
            _fullTree = new List<BlockTreeNode>();
            TreeSummary.Text = string.Empty;
            ApplyTreeFilter();
            ReloadButton.IsEnabled = false;
            TablesButton.IsEnabled = false;
            SetStatus($"Failed to load {Path.GetFileName(path)}: {ex.Message}", StatusKind.Error);
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
            SetEmptyState(
                PlaceholderIcon, PlaceholderTitle, PlaceholderDesc, PlaceholderPanel,
                FASymbol.ContactInfo,
                "Not a dynamic block",
                "This block has no evaluation graph, so it has no parameters.",
                visible: true);
            return;
        }

        EvalStatus.Text = model.EvaluationOk
            ? $"Evaluation OK ({model.GripCount} grip(s) activated)"
            : "Evaluation FAILED — values may be incomplete.";

        _properties = model.Properties;
        PropertyGrid.ItemsSource = model.Properties;
        SetEmptyState(
            PlaceholderIcon, PlaceholderTitle, PlaceholderDesc, PlaceholderPanel,
            FASymbol.ContactInfo,
            "No parameters",
            "This dynamic block has no parameters.",
            visible: model.Properties.Count == 0);
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
    /// Opens the block properties tables dialog for the loaded document
    /// (the decoded "display table" objects), or notes in the status bar when
    /// the document holds none.
    /// </summary>
    private void OnTablesClick(object? sender, RoutedEventArgs e)
    {
        if (_document is null)
        {
            return;
        }

        List<BptTableItem> items = BlockPropertiesTableModel.Build(_document);
        if (items.Count == 0)
        {
            SetStatus("No block properties tables in this document.", StatusKind.Neutral);
            return;
        }

        OpenBlockPropertiesTables(items);
    }

    private void OpenBlockPropertiesTables(List<BptTableItem> items)
    {
        if (_bptDialog is { IsVisible: true })
        {
            _bptDialog.Activate();
            return;
        }

        _bptDialog = new BlockPropertiesTableDialog(items);
        _bptDialog.Closed += (_, _) => _bptDialog = null;
        _bptDialog.Show();
    }

    /// <summary>
    /// Verification helper (used by the --screenshot mode): opens the block
    /// properties tables dialog (modeless) so it can be captured headlessly.
    /// Returns the dialog window, or null when the document holds no tables.
    /// </summary>
    public TopLevel? OpenBlockPropertiesTablesForVerification()
    {
        if (_document is null)
        {
            return null;
        }

        List<BptTableItem> items = BlockPropertiesTableModel.Build(_document);
        if (items.Count == 0)
        {
            return null;
        }

        OpenBlockPropertiesTables(items);
        return _bptDialog;
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
        _nodeViewerDialog = dialog;
        dialog.Closed += (_, _) => _nodeViewerDialog = null;
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
        // The header toggle is a plain button (no IsChecked), so read the current
        // variant and flip dark<->light. High-contrast (chosen from the flyout)
        // flips to dark.
        ThemeVariant current = Application.Current?.RequestedThemeVariant ?? ThemeVariant.Dark;
        ApplyTheme(current == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark);
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
        ThemeLightItem.IsChecked = !isDark && !isHc;
        ThemeDarkItem.IsChecked = isDark;
        ThemeHcItem.IsChecked = isHc;

        // A variant change moves the text brushes too, so refresh the
        // value-foreground converter as well as the accent-dependent views.
        RefreshThemeDependentViews(refreshValueForeground: true);
    }

    /// <summary>
    /// Re-applies the theme-dependent visuals after a theme change: invalidates
    /// the cached theme brushes, re-applies the open node viewer's accent ring
    /// and mini-map card, and (for a variant change) re-binds the property grid
    /// so the value-foreground converter picks up the new theme's text brushes.
    /// </summary>
    private void RefreshThemeDependentViews(bool refreshValueForeground)
    {
        ThemeResources.Refresh();
        _nodeViewerDialog?.RefreshThemeBrushes();

        if (refreshValueForeground && _properties is not null)
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
            "AccentBlue" => ThemeResources.AccentBlue,
            "AccentRed" => ThemeResources.AccentRed,
            "AccentGreen" => ThemeResources.AccentGreen,
            "AccentPurple" => ThemeResources.AccentPurple,
            _ => null,
        };

        // 3.x: the accent is set on the theme instance (6 variants are
        // pregenerated); null restores the system/default accent.
        if (App.Theme is not null)
        {
            App.Theme.CustomAccentColor = accent;
        }

        // An accent change moves the accent brush (not the text brushes), so
        // refresh the accent-dependent views without re-binding the grid.
        RefreshThemeDependentViews(refreshValueForeground: false);
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
            StatusKind.Error => ThemeResources.StatusError,
            StatusKind.Success => ThemeResources.StatusSuccess,
            _ => null, // revert to the styled (tertiary) default
        };
    }

}
