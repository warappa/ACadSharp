using System;
using System.Linq;

if (args.Length > 0 && args[0] == "--analyze")
{
    foreach (string f in args.Skip(1))
    {
        AnalyzePng.Analyze(f);
    }
}
else if (args.Length > 0 && args[0] == "--diag")
{
    foreach (string f in args.Skip(1))
    {
        DiagPorts.Run(f);
    }
}
else if (args.Length > 0 && args[0] == "--graph")
{
    foreach (string f in args.Skip(1))
    {
        DumpGraph.Run(f);
    }
}
else if (args.Length > 0)
{
    foreach (string f in args)
    {
        CheckAll.Run(f);
    }
}
else
{
    Console.WriteLine("usage: ui-check [--analyze <png>...] [--diag <file>...] [--graph <file>...] [<file.dwg|file.dxf>...]");
    Console.WriteLine("  (default: audit edge layout — provider-to-the-right-of-consumer anomalies)");
}
