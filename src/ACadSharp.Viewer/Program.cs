using ACadSharp;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Viewer.Services;
using Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
