using ACadSharp;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
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
            int code = Screenshot.Run(args[1], args.Length >= 3 ? args[2] : null);
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
/// Usage: --screenshot &lt;out.png&gt; [file.dwg|file.dxf]
/// </summary>
static class Screenshot
{
    public static int Run(string outputPath, string? filePath)
    {
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

                // Phase 2: render + capture. CaptureRenderedFrame internally pumps
                // (RunJobs + ForceRenderTimerTick until stable), so a few retries
                // cover the window's open + first paint.
                var frame = window.CaptureRenderedFrame();
                for (int i = 0; i < 30 && frame is null; i++)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                    frame = window.CaptureRenderedFrame();
                    Thread.Sleep(10);
                }

                if (frame is null)
                {
                    Log($"after capture loop: visible={window.IsVisible} bounds={window.Bounds} frame=null");
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
