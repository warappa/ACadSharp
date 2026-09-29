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
    // The expected node-box colors, per theme (the design tokens in
    // DesignTokens.axaml): the Dark values are the canonical palette, the
    // Light values are the same hues moved to darker neutrals.
    static readonly (string name, byte r, byte g, byte b)[] DarkColors =
    {
        ("Parameter", 0x3D, 0x7E, 0xBF),
        ("Grip", 0x3D, 0x9E, 0x5F),
        ("Action", 0xC7, 0x7B, 0x3D),
        ("Component", 0x7A, 0x7A, 0x7A),
        ("FeedbackOrange", 0xE8, 0xA3, 0x3D),
    };

    static readonly (string name, byte r, byte g, byte b)[] LightColors =
    {
        ("Parameter", 0x0F, 0x6C, 0xBD),
        ("Grip", 0x0F, 0x7B, 0x0F),
        ("Action", 0x9D, 0x5D, 0x00),
        ("Component", 0x6E, 0x6E, 0x6E),
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

    public static void Analyze(string path, string palette = "dark")
    {
        (string name, byte r, byte g, byte b)[] colors =
            string.Equals(palette, "light", StringComparison.OrdinalIgnoreCase) ? LightColors : DarkColors;
        Console.WriteLine($"palette: {(colors == LightColors ? "light" : "dark")}");
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
        foreach (var c in colors) { hits[c.name] = new List<(int, int)>(); }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var (r, g, b) = Pixel(x, y);
                for (int i = 0; i < colors.Length; i++)
                {
                    var c = colors[i];
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

    /// <summary>
    /// Crops a region of a screenshot to a new PNG (for visual inspection of a
    /// small area: a port-label row, a node box, a legend).
    /// </summary>
    public static void Crop(string path, int x, int y, int w, int h, string outPath)
    {
        Avalonia.AppBuilder.Configure<TestApp>()
            .UseHeadless(new Avalonia.Headless.AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .SetupWithoutStarting();
        var bmp = new Bitmap(path);
        int W = bmp.PixelSize.Width, H = bmp.PixelSize.Height;
        if (x < 0) { x = 0; }
        if (y < 0) { y = 0; }
        if (x + w > W) { w = W - x; }
        if (y + h > H) { h = H - y; }
        if (w <= 0 || h <= 0)
        {
            Console.WriteLine($"crop: bad region ({x},{y},{w}x{h}) in {W}x{H}");
            return;
        }

        // CopyPixels reads the whole bitmap into a full buffer; keep only the
        // crop rows (Bgra8888, 4 bytes/px, row stride w*4) as RGB.
        var full = new Buf(W * H * 4) { RowBytes = W * 4, Size = new PixelSize(W, H) };
        bmp.CopyPixels(full);
        var rgb = new byte[w * h * 3];
        for (int j = 0; j < h; j++)
        {
            for (int i = 0; i < w; i++)
            {
                int si = (y + j) * W * 4 + i * 4;
                int di = (j * w + i) * 3;
                rgb[di] = full.Data[si + 2]; // B -> R
                rgb[di + 1] = full.Data[si + 1]; // G -> G
                rgb[di + 2] = full.Data[si]; // R -> B
            }
        }

        // Avalonia 12 has no Bitmap-from-pixels ctor / PNG encoder, so encode
        // the crop by hand: signature + IHDR + IDAT (zlib) + IEND.
        using var fs = System.IO.File.Create(outPath);
        PngEncode(fs, w, h, rgb);
        Console.WriteLine($"saved {outPath} ({w}x{h}) from {path}");
    }

    static void PngEncode(System.IO.Stream s, int w, int h, byte[] rgb)
    {
        s.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13]
        {
            (byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w,
            (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h,
            8, 2, 0, 0, 0, // 8-bit truecolor (RGB), no interlace
        };
        WriteChunk(s, "IHDR", ihdr);

        // zlib = 0x78 0x9C + raw deflate + adler32 (big-endian).
        var raw = new byte[h * (1 + w * 3)];
        for (int j = 0; j < h; j++)
        {
            raw[j * (1 + w * 3)] = 0; // filter: none
            System.Buffer.BlockCopy(rgb, j * w * 3, raw, j * (1 + w * 3) + 1, w * 3);
        }
        var deflated = new System.IO.MemoryStream();
        using (var d = new System.IO.Compression.DeflateStream(deflated, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            d.Write(raw);
        }
        var zlib = new byte[2 + (int)deflated.Length + 4];
        zlib[0] = 0x78;
        zlib[1] = 0x9C;
        System.Buffer.BlockCopy(deflated.ToArray(), 0, zlib, 2, (int)deflated.Length);
        // adler32 = (b << 16) | a, big-endian: a = (1 + sum) mod 65521,
        // b = (sum of a) mod 65521 (both 16-bit).
        uint a = 1, b = 0;
        foreach (byte x in raw)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }
        zlib[zlib.Length - 4] = (byte)((b >> 8) & 0xFF);
        zlib[zlib.Length - 3] = (byte)(b & 0xFF);
        zlib[zlib.Length - 2] = (byte)((a >> 8) & 0xFF);
        zlib[zlib.Length - 1] = (byte)(a & 0xFF);
        WriteChunk(s, "IDAT", zlib);
        WriteChunk(s, "IEND", Array.Empty<byte>());
    }

    static void WriteChunk(System.IO.Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        s.Write(len);
        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type, 0, 4, body, 0);
        System.Buffer.BlockCopy(data, 0, body, 4, data.Length);
        s.Write(body);
        var crc = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
        s.Write(crc);
    }

    static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc >> 1) ^ ((crc & 1) * 0xEDB88320U);
            }
        }
        return crc ^ 0xFFFFFFFF;
    }
}
