using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

class TestApp : Avalonia.Application { }

// Scans a screenshot for the node-box colors and reports their bounding boxes.
static class AnalyzePng
{
    static readonly (string name, byte r, byte g, byte b)[] Colors =
    {
        ("Parameter", 0x3D, 0x7E, 0xBF),
        ("Grip", 0x3D, 0x9E, 0x5F),
        ("Action", 0xC7, 0x7B, 0x3D),
        ("Component", 0x7A, 0x7A, 0x7A),
        ("FeedbackOrange", 0xE8, 0xA3, 0x3D),
    };

    class Buf : ILockedFramebuffer
    {
        readonly byte[] _data;
        public GCHandle Handle { get; }
        public Buf(int size)
        {
            _data = new byte[size];
            Handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        }
        public IntPtr Address => Handle.AddrOfPinnedObject();
        public byte[] Data => _data;
        public AlphaFormat AlphaFormat => AlphaFormat.Premul;
        public Vector Dpi => new Vector(96, 96);
        public PixelFormat Format => PixelFormat.Bgra8888;
        public int RowBytes { get; set; }
        public PixelSize Size { get; set; }
        public void Dispose() { Handle.Free(); }
    }

    public static void Analyze(string path)
    {
        Avalonia.AppBuilder.Configure<TestApp>()
            .UseHeadless(new Avalonia.Headless.AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .SetupWithoutStarting();
        var bmp = new Bitmap(path);
        int w = bmp.PixelSize.Width, h = bmp.PixelSize.Height;
        using var buf = new Buf(w * h * 4) { RowBytes = w * 4, Size = new PixelSize(w, h) };
        bmp.CopyPixels(buf);
        byte[] data = buf.Data;
        (byte r, byte g, byte b) Pixel(int x, int y)
        {
            int i = y * buf.RowBytes + x * 4;
            return (data[i + 2], data[i + 1], data[i]); // BGRA -> RGB
        }
        var hits = new Dictionary<string, List<(int x, int y)>>();
        foreach (var c in Colors) { hits[c.name] = new List<(int, int)>(); }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var (r, g, b) = Pixel(x, y);
                for (int i = 0; i < Colors.Length; i++)
                {
                    var c = Colors[i];
                    if (Math.Abs(r - c.r) <= 6 && Math.Abs(g - c.g) <= 6 && Math.Abs(b - c.b) <= 6)
                    {
                        hits[c.name].Add((x, y));
                    }
                }
            }
        }
        foreach (var (name, pts) in hits)
        {
            if (pts.Count == 0) { continue; }
            Console.WriteLine($"{name}: {pts.Count} px");
            var cells = new Dictionary<(int cx, int cy), List<(int x, int y)>>();
            foreach (var (x, y) in pts)
            {
                var key = (x / 40, y / 40);
                if (!cells.TryGetValue(key, out var list)) { cells[key] = list = new List<(int, int)>(); }
                list.Add((x, y));
            }
            var cellKeys = new List<(int cx, int cy)>(cells.Keys);
            var compOf = new int[cellKeys.Count];
            for (int i = 0; i < compOf.Length; i++) { compOf[i] = -1; }
            var boxes = new List<(int minX, int minY, int maxX, int maxY, int count)>();
            for (int i = 0; i < cellKeys.Count; i++)
            {
                if (compOf[i] >= 0) { continue; }
                int id = boxes.Count;
                compOf[i] = id;
                var queue = new Queue<int>();
                queue.Enqueue(i);
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue, count = 0;
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    foreach (var (x, y) in cells[cellKeys[cur]])
                    {
                        count++;
                        if (x < minX) { minX = x; }
                        if (y < minY) { minY = y; }
                        if (x > maxX) { maxX = x; }
                        if (y > maxY) { maxY = y; }
                    }
                    for (int j = 0; j < cellKeys.Count; j++)
                    {
                        if (compOf[j] >= 0) { continue; }
                        if (Math.Abs(cellKeys[j].cx - cellKeys[cur].cx) <= 1 && Math.Abs(cellKeys[j].cy - cellKeys[cur].cy) <= 1)
                        {
                            compOf[j] = id;
                            queue.Enqueue(j);
                        }
                    }
                }
                boxes.Add((minX, minY, maxX, maxY, count));
            }
            boxes.Sort((a, b) => a.minX - b.minX);
            foreach (var b in boxes)
            {
                if (b.maxX - b.minX < 50 || b.maxY - b.minY < 20) { continue; }
                Console.WriteLine($"  box: x=[{b.minX}..{b.maxX}]  y=[{b.minY}..{b.maxY}]  center=({(b.minX + b.maxX) / 2},{(b.minY + b.maxY) / 2})");
            }
        }
    }
}
