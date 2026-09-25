using ACadSharp;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Controls;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ACadSharp.Viewer;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--smoke")
        {
            int code = SmokeTest.Run(args[1]);
            Environment.Exit(code);
            return;
        }

        if (args.Length >= 2 && args[0] == "--screenshot")
        {
            string? mode = args.Length >= 4 ? args[3] : null;
            int code = Screenshot.Run(args[1], args.Length >= 3 ? args[2] : null, mode);
            Environment.Exit(code);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

/// <summary>
/// Headless verification mode: loads a .dwg/.dxf file, prints the block
/// hierarchy tree, evaluates every dynamic block's graph, and prints the
/// resulting node values. Exits 0 on success, 1 on failure.
/// </summary>
static class SmokeTest
{
    public static int Run(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"file not found: {filePath}");
            return 1;
        }

        CadDocument doc;
        try
        {
            doc = CadFileService.LoadFile(filePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"load failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Loaded {Path.GetFileName(filePath)}: {doc.BlockRecords.Count} block record(s)");

        // 1. Block hierarchy tree.
        var tree = BlockTreeModel.Build(doc);
        Console.WriteLine();
        Console.WriteLine("=== block hierarchy ===");
        foreach (BlockTreeNode root in tree)
        {
            PrintNode(root, 0);
        }

        // 2. Evaluate every dynamic block.
        int dynamicCount = 0;
        int failed = 0;
        foreach (BlockRecord block in doc.BlockRecords)
        {
            EvaluationGraph? graph = block.EvaluationGraph;
            if (graph is null)
            {
                continue;
            }

            dynamicCount++;
            Console.WriteLine();
            Console.WriteLine($"=== evaluation: block '{block.Name}' ===");

            List<int> gripIndices = graph.Nodes
                .Where(n => n.Expression is BlockGrip)
                .Select(n => n.Index)
                .ToList();
            graph.Activate(gripIndices);

            bool ok = graph.Evaluate();
            Console.WriteLine($"Evaluate: {(ok ? "OK" : "FAILED")}  (activated {gripIndices.Count} grip(s))");
            if (!ok)
            {
                failed++;
                continue;
            }

            foreach (EvaluationGraph.Node node in graph.Nodes.OrderBy(n => n.Index))
            {
                EvaluationExpression? expr = node.Expression;
                if (expr is null)
                {
                    continue;
                }

                string value = expr.CurrentValue.Type == EvaluationValueType.None
                    ? "<unset>"
                    : expr.CurrentValue.ToString();
                Console.WriteLine($"  node {node.Index,2}: {expr.GetType().Name,-28} value={value}");
            }

            // 3. Properties (label / description / value).
            BlockModel? model = BlockModel.Create(block);
            if (model is null)
            {
                continue;
            }

            Console.WriteLine();
            Console.WriteLine("--- properties ---");
            foreach (PropertyItem property in model.Properties)
            {
                Console.WriteLine($"  {property.Name,-20} [{property.Description}] = {property.ValueText}");
            }

            // 4. Ancestor subgraph sizes (node viewer input).
            Console.WriteLine();
            Console.WriteLine("--- ancestor subgraphs ---");
            foreach (PropertyItem property in model.Properties)
            {
                GraphModel.Result sub = GraphModel.BuildAncestors(graph, property.NodeIndex);
                Console.WriteLine($"  {property.Name,-20} -> {sub.Nodes.Count} node(s), {sub.Edges.Count} edge(s), maxDepth={sub.MaxDepth}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Smoke result: {dynamicCount} dynamic block(s), {failed} evaluation failure(s)");
        return failed == 0 ? 0 : 1;
    }

    private static void PrintNode(BlockTreeNode node, int depth)
    {
        string indent = new(' ', depth * 2);
        Console.WriteLine($"{indent}{node.DisplayName}");
        foreach (BlockTreeNode child in node.Children)
        {
            PrintNode(child, depth + 1);
        }
    }
}

/// <summary>
/// Headless screenshot mode: renders the main window on the Avalonia.Headless
/// platform (CPU-only; no display, X11, or GPU required) and saves a PNG.
/// Usage: --screenshot &lt;out.png&gt; [file.dwg|file.dxf] [dialog|tip|flyout|interact|minimap]
/// The optional "dialog" argument opens the node viewer for the first
/// dynamic block after the file loads and captures that dialog instead.
/// The optional "tip" argument force-shows the first-run teaching tip
/// (regardless of the first-run flag) and captures the main window.
/// The optional "flyout" argument opens the settings flyout and captures
/// the main window. The "hc" and "accent" arguments apply the high-contrast
/// theme / a red accent (the settings-flyout operations) and capture the
/// main window. The "interact" argument opens the node viewer and simulates
/// a user (wheel zoom, drag pan, click select, hover) through the real
/// input pipeline before capturing the dialog. The "minimap" argument opens
/// the node viewer, zooms in (so the visible-area rectangle is clearly
/// smaller than the whole graph), and navigates through the mini-map
/// (press + drag), verifying both the rectangle tracking and the
/// mini-map navigation.
/// </summary>
static class Screenshot
{
    public static int Run(string outputPath, string? filePath, string? mode = null)
    {
        bool openDialog = mode == "dialog";
        bool showTip = mode == "tip";
        bool showFlyout = mode == "flyout";
        bool applyHc = mode == "hc";
        bool applyAccent = mode == "accent";
        bool interact = mode == "interact";
        bool zoombug = mode == "zoombug";
        bool minimap = mode == "minimap";
        if (filePath is not null && !File.Exists(filePath))
        {
            Console.Error.WriteLine($"file not found: {filePath}");
            return 1;
        }

        // Diagnostic log (written from the UI/dedicated thread).
        var diag = new List<string>();
        void Log(string msg)
        {
            diag.Add($"{DateTime.Now:HH:mm:ss.fff} {msg}");
            Console.Error.WriteLine("[diag] " + msg);
        }

        Exception? error = null;
        int result = 1;

        // The default headless options (UseHeadlessDrawing=true) install a no-op
        // render-interface stub that never locks the window's framebuffer, so
        // GetLastRenderedFrame() would always return null. Instead we build the
        // app manually with UseHeadlessDrawing=false + the real Skia renderer:
        // Skia's FramebufferRenderTarget locks the headless window's framebuffer
        // (Lock()) and draws real pixels into it, which CaptureRenderedFrame()
        // can then read back.
        //
        // Everything must run on one dedicated thread: the Dispatcher created
        // during SetupWithoutStarting() binds to that thread and becomes the
        // UI thread (Dispatcher.UIThread).
        var done = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                AppBuilder.Configure<App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                    .UseSkia()
                    .SetupWithoutStarting();
                Log($"setup done; thread={Thread.CurrentThread.ManagedThreadId} checkAccess={Avalonia.Threading.Dispatcher.UIThread.CheckAccess()}");

                // The app's desktop lifetime (if any) already creates a MainWindow;
                // reuse it rather than creating a second window.
                // (Avalonia 12 removed Application.Windows.)
                MainWindow? window = (Application.Current?.ApplicationLifetime
                    as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;
                window ??= new MainWindow();

                window.Show();
                Log($"after Show: visible={window.IsVisible} impl={window.PlatformImpl is not null} bounds={window.Bounds}");

                // Kick off the file load (runs I/O on the thread pool, UI on the
                // dispatcher). Do not await — we pump the dispatcher below.
                Task? loadTask = null;
                if (filePath is not null)
                {
                    loadTask = window.LoadFileAsync(filePath);
                    Log($"load kicked off");
                }

                // Phase 1: pump the dispatcher until the file load completes
                // (thread-pool work must finish, then its continuation — posted to
                // the dispatcher — must run). Sleep briefly between pumps.
                if (loadTask is not null)
                {
                    for (int i = 0; i < 400 && !loadTask.IsCompleted; i++)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(20);
                    }
                    Log($"load completed={loadTask.IsCompleted}");
                }

                // For the main-window capture, select the first dynamic block
                // so the property grid is populated (mirrors what a user does).
                if (filePath is not null && !openDialog && !showTip && !showFlyout && !interact)
                {
                    var selected = window.SelectFirstDynamicNode();
                    Log($"first dynamic node selected={selected is not null}");
                    for (int i = 0; i < 10; i++)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(10);
                    }
                }

                // Optionally force the first-run teaching tip open (verification).
                if (showTip)
                {
                    window.ForceShowIntroTip();
                    for (int i = 0; i < 20; i++)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(10);
                    }
                    Log("tip forced open");
                }

                // Optionally open the settings flyout (verification). The popup
                // is windowless in Avalonia 12 (overlay layer), so it renders
                // into the main window's frame.
                if (showFlyout)
                {
                    window.ForceOpenSettingsFlyout();
                    for (int i = 0; i < 30; i++)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(10);
                    }
                    Log("settings flyout forced open");
                }

                // Optionally apply the high-contrast theme or a red accent
                // (the settings-flyout operations, for verification).
                if (applyHc)
                {
                    window.ApplyThemeAndAccentForVerification(FluentAvaloniaTheme.HighContrastTheme, null);
                    for (int i = 0; i < 20; i++)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(10);
                    }
                    Log("high contrast applied");
                }
                else if (applyAccent)
                {
                    window.ApplyThemeAndAccentForVerification(ThemeVariant.Dark, Avalonia.Media.Color.Parse("#D13438"));
                    for (int i = 0; i < 20; i++)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(10);
                    }
                    Log("red accent applied");
                }

                // Optionally open the node viewer dialog and capture it instead.
                TopLevel? dialog = null;
                if (openDialog || interact || zoombug || minimap)
                {
                    dialog = window.OpenFirstDynamicNodeViewer();
                    Log($"node viewer opened={dialog is not null}");
                    if (dialog is not null)
                    {
                        // Pump until the dialog is visible and its auto-fit
                        // (posted at Background priority) has run.
                        for (int i = 0; i < 30 && !dialog.IsVisible; i++)
                        {
                            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                            Thread.Sleep(20);
                        }
                        for (int i = 0; i < 10; i++)
                        {
                            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                            Thread.Sleep(10);
                        }
                        Log($"dialog visible={dialog.IsVisible} bounds={dialog.Bounds}");
                        if (dialog is NodeViewerDialog nodeViewerForMiniMapLog)
                        {
                            Log($"minimap: {nodeViewerForMiniMapLog.MiniMap.MiniMapStateForVerification}");
                        }

                        // Verification: simulate a user through the real input
                        // pipeline (Avalonia.Headless extensions on TopLevel):
                        // wheel zoom, drag pan, click select, hover.
                        if (interact && dialog is NodeViewerDialog nodeViewer)
                        {
                            void Pump(int n)
                            {
                                for (int i = 0; i < n; i++)
                                {
                                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                                    Thread.Sleep(10);
                                }
                            }

                            // 1. Zoom in two notches at a point inside the graph.
                            Point center = new Point(200, 150);
                            nodeViewer.MouseWheel(center, new Vector(0, 1));
                            nodeViewer.MouseWheel(center, new Vector(0, 1));
                            Pump(10);

                            // 2. Pan: press, drag 120/80 px, release (content moves
                            //    120 px left and 80 px up).
                            nodeViewer.MouseDown(center, Avalonia.Input.MouseButton.Left);
                            nodeViewer.MouseMove(center + new Vector(120, 80));
                            nodeViewer.MouseUp(center + new Vector(120, 80), Avalonia.Input.MouseButton.Left);
                            Pump(10);

                            // 3. Select the first node box (a press without drag).
                            Point? box0 = nodeViewer.Graph.GetNodeBoxCenter(nodeViewer, 0);
                            Log($"box0={box0}");
                            if (box0 is not null)
                            {
                                nodeViewer.MouseDown(box0.Value, Avalonia.Input.MouseButton.Left);
                                nodeViewer.MouseUp(box0.Value, Avalonia.Input.MouseButton.Left);
                                Pump(10);
                            }

                            // 4. Hover the second node box.
                            Point? box1 = nodeViewer.Graph.GetNodeBoxCenter(nodeViewer, 1);
                            Log($"box1={box1}");
                            if (box1 is not null)
                            {
                                nodeViewer.MouseMove(box1.Value);
                                Pump(10);
                            }

                            // 5. Regression check (issue: the wheel only worked over
                            //    the graph content, the empty viewport area was a
                            //    dead zone the scroll viewer consumed): the canvas
                            //    now fills the viewport, so the wheel must zoom at
                            //    a point in the empty area too.
                            Point empty = nodeViewer.Graph.TranslatePoint(
                                new Point(700, 400), nodeViewer) ?? new Point(700, 400);
                            double scaleBefore = nodeViewer.Graph.Scale;
                            nodeViewer.MouseWheel(empty, new Vector(0, 1));
                            nodeViewer.MouseWheel(empty, new Vector(0, 1));
                            Pump(10);
                            Log($"empty-area wheel: scale {scaleBefore} -> {nodeViewer.Graph.Scale}");

                            // 6. Regression check (issue: drag-pan was dead in the
                            //    empty area): zoom in until the content overflows the
                            //    viewport, then drag-pan from the empty area.
                            while (nodeViewer.Graph.Scale < 2.0)
                            {
                                nodeViewer.Graph.ZoomIn();
                                Pump(5);
                            }
                            Vector offsetBefore = nodeViewer.Graph.ScrollOffsetForVerification;
                            nodeViewer.MouseDown(empty, Avalonia.Input.MouseButton.Left);
                            nodeViewer.MouseMove(empty + new Vector(100, 60));
                            nodeViewer.MouseUp(empty + new Vector(100, 60), Avalonia.Input.MouseButton.Left);
                            Pump(10);
                            Log($"empty-area pan: offset {offsetBefore} -> {nodeViewer.Graph.ScrollOffsetForVerification}");
                            Log($"state: {nodeViewer.Graph.ScrollStateForVerification}");

                            // 7. Fit the graph back into view so the capture shows it
                            //    (the regression checks above zoomed in and panned it
                            //    away). FitToView sets scale 1.0, which shrinks the
                            //    canvas to the viewport and clamps the offset to (0,0).
                            nodeViewer.Graph.FitToView();
                            Pump(10);
                            Log($"after fit: {nodeViewer.Graph.ScrollStateForVerification}");

                            // 8. Hover a point on the first labeled edge: the edge,
                            //    its arrowhead, and its label must all highlight
                            //    (the label-highlight regression check).
                            Point? edgePoint = nodeViewer.Graph.GetLabeledEdgePoint(nodeViewer);
                            Log($"labeled edge point={edgePoint}");
                            if (edgePoint is not null)
                            {
                                var hit = nodeViewer.InputHitTest(edgePoint.Value, enabledElementsOnly: false);
                                Log($"edge hover hit={hit?.GetType().Name}");
                                nodeViewer.MouseMove(edgePoint.Value);
                                Pump(10);
                            }
                        }

                        // Reproduce the reported bug: zoom in with the wheel in the
                        // empty area BELOW the nodes until the content overflows the
                        // viewport (scrollbars appear), then scroll back to the
                        // top-left. The nodes must come back into view.
                        if (zoombug && dialog is NodeViewerDialog zoomBugViewer)
                        {
                            void PumpZb(int n)
                            {
                                for (int i = 0; i < n; i++)
                                {
                                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                                    Thread.Sleep(10);
                                }
                            }

                            Log($"initial: scale={zoomBugViewer.Graph.Scale} {zoomBugViewer.Graph.ScrollStateForVerification}");
                            Log($"initial: box0 center={zoomBugViewer.Graph.GetNodeBoxCenter(zoomBugViewer, 0)}");

                            // The nodes sit in the top-left; zoom in the empty area
                            // below/right of them (mirrors the user's report).
                            Point low = zoomBugViewer.Graph.TranslatePoint(
                                new Point(700, 450), zoomBugViewer) ?? new Point(700, 450);
                            for (int i = 0; i < 8; i++)
                            {
                                zoomBugViewer.MouseWheel(low, new Vector(0, 1));
                                PumpZb(5);
                                Log($"wheel {i + 1}/8: scale={zoomBugViewer.Graph.Scale:0.###} {zoomBugViewer.Graph.ScrollStateForVerification}");
                            }
                            Log($"after zoom: box0 center={zoomBugViewer.Graph.GetNodeBoxCenter(zoomBugViewer, 0)}");

                            // The user scrolls to the top-left (both scrollbars to start).
                            zoomBugViewer.Graph.ScrollToOriginForVerification();
                            PumpZb(10);
                            Log($"after scroll-to-origin: scale={zoomBugViewer.Graph.Scale:0.###} {zoomBugViewer.Graph.ScrollStateForVerification}");
                            Log($"after scroll-to-origin: box0 center={zoomBugViewer.Graph.GetNodeBoxCenter(zoomBugViewer, 0)}");
                        }

                        // Verify the mini-map: zoom in (the visible-area
                        // rectangle must shrink to a fraction of the whole
                        // graph), then navigate through the mini-map (a
                        // press + drag re-centers the main view on the
                        // content point under the pointer).
                        if (minimap && dialog is NodeViewerDialog miniMapViewer)
                        {
                            void PumpMm(int n)
                            {
                                for (int i = 0; i < n; i++)
                                {
                                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                                    Thread.Sleep(10);
                                }
                            }

                            Log($"before: {miniMapViewer.Graph.ScrollStateForVerification}");
                            Log($"minimap before: {miniMapViewer.MiniMap.MiniMapStateForVerification}");

                            // Drag the first node box in the main view (left
                            // button, +60/+40, at the fit scale of 1.0): the
                            // node's mini-map box must follow (its offset
                            // appears in the mini-map state).
                            Point? box0 = miniMapViewer.Graph.GetNodeBoxCenter(miniMapViewer, 0);
                            Log($"box0={box0}");
                            if (box0 is not null)
                            {
                                miniMapViewer.MouseDown(box0.Value, Avalonia.Input.MouseButton.Left);
                                miniMapViewer.MouseMove(box0.Value + new Vector(60, 40));
                                miniMapViewer.MouseUp(box0.Value + new Vector(60, 40), Avalonia.Input.MouseButton.Left);
                                PumpMm(10);
                                Log($"after node drag: {miniMapViewer.MiniMap.MiniMapStateForVerification}");
                            }

                            // Zoom in three notches at a point inside the graph.
                            Point p = new Point(300, 200);
                            miniMapViewer.MouseWheel(p, new Vector(0, 1));
                            miniMapViewer.MouseWheel(p, new Vector(0, 1));
                            miniMapViewer.MouseWheel(p, new Vector(0, 1));
                            PumpMm(10);
                            Log($"after zoom: {miniMapViewer.Graph.ScrollStateForVerification}");
                            Log($"minimap after zoom: {miniMapViewer.MiniMap.MiniMapStateForVerification}");

                            // Press + drag the center of the mini-map: the
                            // view should re-center on the content point
                            // under the pointer.
                            Point? mmCenter = miniMapViewer.MiniMap.TranslatePoint(
                                new Point(110, 75), miniMapViewer);
                            Log($"minimap center={mmCenter}");
                            if (mmCenter is not null)
                            {
                                miniMapViewer.MouseDown(mmCenter.Value, Avalonia.Input.MouseButton.Left);
                                miniMapViewer.MouseMove(mmCenter.Value + new Vector(40, 20));
                                miniMapViewer.MouseUp(mmCenter.Value + new Vector(40, 20), Avalonia.Input.MouseButton.Left);
                                PumpMm(10);
                                Log($"after minimap drag: {miniMapViewer.Graph.ScrollStateForVerification}");
                                Log($"minimap after drag: {miniMapViewer.MiniMap.MiniMapStateForVerification}");
                            }
                        }
                    }
                }

                // Phase 2: render + capture. CaptureRenderedFrame internally pumps
                // (RunJobs + ForceRenderTimerTick until stable), so a few retries
                // cover the window's open + first paint.
                TopLevel captureTarget = dialog ?? window;
                var frame = captureTarget.CaptureRenderedFrame();
                for (int i = 0; i < 30 && frame is null; i++)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                    frame = captureTarget.CaptureRenderedFrame();
                    Thread.Sleep(10);
                }

                if (frame is null)
                {
                    Log($"after capture loop: visible={captureTarget.IsVisible} bounds={captureTarget.Bounds} frame=null");
                    throw new InvalidOperationException("no frame was rendered");
                }

                // The frame type (IBitmap) is internal to Avalonia 12, so it cannot
                // be named here; a var-inferred value can still call its public Save.
                using FileStream fs = File.Create(outputPath);
                frame.Save(fs, new PngBitmapEncoderOptions());
                Log($"saved frame {frame.PixelSize}");
                result = 0;
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                done.Set();
            }
        });
        thread.Name = "Avalonia-UI"; // .NET 10 dropped the Thread(name) ctor
        thread.Start();

        if (!done.Wait(TimeSpan.FromSeconds(120)))
        {
            Console.Error.WriteLine("screenshot timed out");
            return 1;
        }

        // Flush diagnostics.
        try
        {
            File.WriteAllLines(Path.Combine(Path.GetDirectoryName(outputPath) ?? ".", "diag.txt"), diag);
        }
        catch
        {
            // Diagnostics are best-effort.
        }

        if (error is not null)
        {
            Console.Error.WriteLine($"screenshot failed: {error.Message}");
            return 1;
        }

        Console.WriteLine($"screenshot saved: {outputPath}");
        return result;
    }
}
