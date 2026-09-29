using System;
using System.Collections.Generic;
using System.Linq;

if (args.Length > 0 && args[0] == "--analyze")
{
    // --light switches the expected palette to the Light-theme node colors
    // (for the "dialog,light" screenshot mode); default is the Dark palette.
    bool light = false;
    var files = new List<string>();
    foreach (string a in args.Skip(1))
    {
        if (a == "--light") { light = true; continue; }
        files.Add(a);
    }
    foreach (string f in files)
    {
        AnalyzePng.Analyze(f, light ? "light" : "dark");
    }
}
else if (args.Length > 0 && args[0] == "--crop")
{
    // --crop <in.png> <x> <y> <w> <h> <out.png>
    if (args.Length < 7)
    {
        Console.WriteLine("usage: ui-check --crop <in.png> <x> <y> <w> <h> <out.png>");
    }
    else
    {
        AnalyzePng.Crop(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), args[6]);
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
    Console.WriteLine("usage: ui-check [--analyze <png>... [--light]] [--crop <in.png> <x> <y> <w> <h> <out.png>] [--diag <file>...] [--graph <file>...] [<file.dwg|file.dxf>...]");
    Console.WriteLine("  (default: audit edge layout — provider-to-the-right-of-consumer anomalies)");
}
