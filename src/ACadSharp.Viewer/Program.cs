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

        if (args.Length >= 2 && args[0] == "--edges")
        {
            int code = EdgeInspector.Run(args[1], args.Length >= 3 ? args[2] : null);
            Environment.Exit(code);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Linux runs on the X11 backend — also inside a Wayland session, where
            // it goes through XWayland (the native Avalonia.Wayland backend is not
            // referenced). Most Linux window managers, KWin included, decorate the
            // frame server-side, which stacked the WM's caption on top of our own
            // title strip (a double titlebar). EnableDrawnDecorations hands the
            // frame to Avalonia: it draws the border, shadow and resize grips and
            // stops requesting WM decorations. Combined with
            // Window.ExtendClientAreaToDecorationsHint (MainWindow.axaml) the
            // caption bar belongs to the app, so the Fluent title strip is the only
            // titlebar — the same result as the Windows and macOS builds.
            .With(new X11PlatformOptions
            {
                // ACADSHARP_VIEWER_X11_CSD=0 hands the frame back to the window
                // manager (the pre-Avalonia-12 look) for A/B comparison.
                EnableDrawnDecorations = !string.Equals(
                    Environment.GetEnvironmentVariable("ACADSHARP_VIEWER_X11_CSD"),
                    "0", StringComparison.OrdinalIgnoreCase),
            })
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

            // The activation seeds: the parameter grips and the properties-table grip
            // (both are user-touchable handles; see EvaluationExpression.IsActivatable).
            List<int> gripIndices = graph.Nodes
                .Where(n => n.Expression is { } e && e.IsActivatable)
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

