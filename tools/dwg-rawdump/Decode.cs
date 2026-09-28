using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Mirrors DwgStreamReaderBase's bit-packing exactly, with position logging.
class BitReader
{
    public byte[] Data;
    public long Bit; // absolute bit position
    public bool Debug = true;

    public BitReader(byte[] data) { this.Data = data; this.Bit = 0; }

    public int ReadRawBit()
    {
        long byteIdx = this.Bit / 8;
        int bitInByte = (int)(this.Bit % 8);
        if (byteIdx >= this.Data.Length) return 0;
        return (this.Data[byteIdx] >> (7 - bitInByte)) & 1;
    }

    public int ReadNBits(int n)
    {
        int v = 0;
        for (int i = 0; i < n; i++)
        {
            v = (v << 1) | this.ReadRawBit();
            this.Bit++;
        }
        return v;
    }

    public int Read2Bits() => this.ReadNBits(2);

    public bool ReadBit() => this.ReadNBits(1) != 0;

    public int ReadByte() => this.ReadNBits(8);

    public int ReadBitLong()
    {
        int tag = this.Read2Bits();
        int v;
        switch (tag)
        {
            case 0: v = this.ReadLE(4); break;
            case 1: v = this.ReadByte(); break;
            case 2: v = 0; break;
            default: throw new Exception($"ReadBitLong tag {tag} @ {this.Bit - 2}");
        }
        return v;
    }

    public short ReadBitShort()
    {
        int tag = this.Read2Bits();
        int v;
        switch (tag)
        {
            case 0: v = this.ReadLE(2); break;
            case 1: v = this.ReadByte(); break;
            case 2: v = 0; break;
            case 3: v = 256; break;
            default: throw new Exception($"ReadBitShort tag {tag}");
        }
        return (short)v;
    }

    public double ReadBitDouble()
    {
        int tag = this.Read2Bits();
        if (Debug) Console.WriteLine($"    [ReadBitDouble @bit {this.Bit - 2}] tag={tag}");
        double v;
        switch (tag)
        {
            case 0:
            {
                int dbgStart = (int)this.Bit;
                ulong ul = 0;
                var raw = new List<int>();
                for (int i = 0; i < 8; i++) { int b = this.ReadNBits(8); raw.Add(b); ul |= (ulong)b << (i * 8); }
                v = BitConverter.Int64BitsToDouble((long)ul);
                if (Debug) Console.WriteLine($"      [dbl@{dbgStart}] bytes=[{string.Join(",", raw)}] ul=0x{ul:X16} v={v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}");
                break;
            }
            case 1: v = 1.0; break;
            case 2: v = 0.0; break;
            default: throw new Exception($"ReadBitDouble tag {tag}");
        }
        return v;
    }

    public int ReadLE(int n)
    {
        int v = 0;
        for (int i = 0; i < n; i++)
        {
            int b = this.ReadNBits(8);
            v |= b << (i * 8);
        }
        return v;
    }

    public void AdvanceTo(long bit) { this.Bit = bit; }
}

class Program
{
    static string F(double d) => d.ToString("0.######", CultureInfo.InvariantCulture);

    static void Main()
    {
        // Handle 3B6 raw class data (byte-aligned start), 1507 bits = 189 bytes (last partial).
        var lines = new[]
        {
            "3F FF FF FF D2 16 BC F1",
            "D8 42 52 16 BE AA 3F FF",
            "FF FF FF FF FB EF C0 00",
            "00 00 00 00 0D E3 CA 90",
            "14 29 01 42 A4 2A A0 00",
            "00 00 00 00 03 82 FE AA",
            "41 93 00 1A 40 1B 80 19",
            "40 18 40 1C 80 10 D4 40",
            "06 90 07 30 07 00 06 C0",
            "06 10 06 30 06 50 06 D0",
            "06 50 06 E0 07 40 05 80",
            "04 35 10 01 A4 01 CC 01",
            "C0 01 B0 01 84 01 8C 01",
            "94 01 B4 01 94 01 B8 01",
            "D0 01 64 01 06 4C 00 65",
            "00 6E 00 67 00 74 00 68",
            "00 47 14 C0 19 40 1D 00",
            "1C C0 08 00 1D 00 1A 00",
            "19 40 08 00 1B 00 19 40",
            "1B 80 19 C0 1D 00 1A 00",
            "08 00 1B C0 19 80 08 00",
            "1D 00 1A 00 19 40 08 00",
            "18 40 1C 80 1C 80 1B C0",
            "1D C0 14 81 38",
        };
        var all = new List<byte>();
        foreach (var ln in lines)
            foreach (var t in ln.Split(' '))
                if (t.Length == 2)
                    all.Add(Convert.FromHexString(t)[0]);
        byte[] data = all.ToArray();
        Console.WriteLine($"data length = {data.Length} bytes = {data.Length*8} bits (expect 1507)");

        var r = new BitReader(data);

        // readEvaluationExpression:
        int unknown = r.ReadBitLong();
        int ev98 = r.ReadBitLong();
        int ev99 = r.ReadBitLong();
        short code = r.ReadBitShort();
        Console.WriteLine($"[expr] unknown={unknown} v98={ev98} v99={ev99} code={code}  @bit {r.Bit}");
        if (code > 0) Console.WriteLine($"   [expr] code={code} > 0 (inline value present)");

        // code 90: Id (MISSED before!)
        int exprId = r.ReadBitLong();
        Console.WriteLine($"[expr] Id={exprId}  @bit {r.Bit}");

        // readBlockElement: (name = text[0])
        Console.WriteLine($"[element] name=<text0> v98={r.ReadBitLong()} v99={r.ReadBitLong()} v1071={r.ReadBitLong()}  @bit {r.Bit}");

        // readBlockParameter:
        bool show = r.ReadBit();
        bool chain = r.ReadBit();
        Console.WriteLine($"[parameter] show={show} chain={chain}  @bit {r.Bit}");

        // readBlock2PtParameter: first, second
        Console.WriteLine($"[2pt] first=({F(r.ReadBitDouble())},{F(r.ReadBitDouble())},{F(r.ReadBitDouble())})  @bit {r.Bit}");
        Console.WriteLine($"[2pt] second=({F(r.ReadBitDouble())},{F(r.ReadBitDouble())},{F(r.ReadBitDouble())})  @bit {r.Bit}");

        // 4 displacement properties
        for (int i = 0; i < 4; i++)
        {
            short n = r.ReadBitShort();
            var parts = new List<string>();
            for (int j = 0; j < n; j++)
            {
                int id = r.ReadBitLong();
                // name = text[k]
                parts.Add($"{id}:<text>");
            }
            Console.WriteLine($"[2pt] disp[{i}]={(n)}[{string.Join(",", parts)}]  @bit {r.Bit}");
        }

        // 4 grip ids
        var grips = new List<int>();
        for (int i = 0; i < 4; i++) grips.Add(r.ReadBitLong());
        short baseLoc = r.ReadBitShort();
        Console.WriteLine($"[2pt] grips=[{string.Join(",", grips)}] baseLoc={baseLoc}  @bit {r.Bit}");

        // linear tail: label, desc, labelOffset, valueSet
        Console.WriteLine($"[linear] label=<text> desc=<text> labelOffset={F(r.ReadBitDouble())}  @bit {r.Bit}");
        // valueSet
        int vsType = r.ReadBitLong();
        double vMin = r.ReadBitDouble();
        double vMax = r.ReadBitDouble();
        double vInc = r.ReadBitDouble();
        short vCount = r.ReadBitShort();
        Console.WriteLine($"[valueSet] type={vsType} min={F(vMin)} max={F(vMax)} inc={F(vInc)} count={vCount}  @bit {r.Bit}");
        for (int i = 0; i < vCount; i++) Console.WriteLine($"   val={F(r.ReadBitDouble())} @bit {r.Bit}");
    }
}
