using System;
using System.Collections.Generic;
using System.Text;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Decodes a <c>BlockPropertiesTable.RawTail</c> into its on-disk structure (header, records,
/// string pool, tail). Self-contained (no library dependency) so it can be lifted into the
/// library. The methods may return <c>null</c> when the schema does not match; the result
/// classes are nullable-disabled (no <c>?</c>) so the file lifts into the library (which has
/// no nullable context). See the README for the verified layout.
/// </summary>
public static class BptBodyDecoder
{
	/// <summary>The decoded trailing string pool (<c>null</c> when the schema does not match).</summary>
	public sealed class PoolResult
	{
		public int Start;
		public int BitCount;
		public string[] Strings;
	}

	/// <summary>The decoded BPT body (<c>null</c> when the schema does not match).</summary>
	public sealed class Body
	{
		public int TotalBits;
		public int HeaderBits;                 // 544 (1kV family)
		public int RecordBits;
		public int PoolStart;                // bit offset of the string pool
		public int PoolBits;
		public int TailBits;                // 33 (1kV)
		public string[] Strings;
		public int[] RecordIndices;          // 7-bit indices (1kV schema)
		public bool RecordSchemaMatched;     // true if the 1kV record schema matched
	}

	/// <summary>
	/// Decodes the trailing string pool: finds the start <c>P</c> at which a
	/// <c>[ReadBitShort length][length*2 UTF-16LE]</c> sequence consumes exactly
	/// <c>(bitCount - tail - P)</c> bits with every string printable, preferring the
	/// <c>P</c> that yields the most strings (the true pool start). Returns null when no
	/// <c>P</c> matches.
	/// </summary>
	public static PoolResult TryDecodeStringPool(byte[] data, int bitCount, int tail = 33)
	{
		if (data is null || bitCount <= 0 || tail >= bitCount) return null;
		int end = bitCount - tail;
		int bestP = -1, bestCount = 0, bestBits = 0;
		string[] bestStrs = null;
		for (int p = end - 1; p >= 0; p--)
		{
			int target = end - p;
			if (target <= 0) continue;
			var r = new BitReader(data, p, end);
			var strs = new List<string>();
			bool ok = true;
			while (r.Bit < r.End)
			{
				int len = r.ReadBitShort();
				if (len < 0 || len > 4000) { ok = false; break; }
				byte[] buf = new byte[len * 2];
				for (int j = 0; j < len * 2; j++) buf[j] = (byte)r.Read(8);
				string s = Encoding.Unicode.GetString(buf);
				if (!AllPrintable(s)) { ok = false; break; }
				strs.Add(s);
			}
			if (ok && r.Bit == r.End && strs.Count > 0 && strs.Count > bestCount)
			{
				bestCount = strs.Count; bestP = p; bestStrs = strs.ToArray(); bestBits = target;
			}
		}
		if (bestP < 0) return null;
		return new PoolResult { Start = bestP, BitCount = bestBits, Strings = bestStrs };
	}

	/// <summary>
	/// Decodes the full 1kV body: 544-bit header + 126-bit records (each with a 7-bit string
	/// index = bit 70 &lt;&lt; 6 | 6-bit[97..102]) + string pool + 33-bit tail. Returns the
	/// structured body, or null when the 1kV record schema does not match.
	/// </summary>
	public static Body Decode(byte[] data, int bitCount)
	{
		if (data is null || bitCount <= 0) return null;
		const int headerBits = 544;
		const int tailBits = 33;
		var body = new Body { TotalBits = bitCount, HeaderBits = headerBits, TailBits = tailBits };

		var pool = TryDecodeStringPool(data, bitCount, tailBits);
		if (pool is null) return null;
		body.PoolStart = pool.Start;
		body.PoolBits = pool.BitCount;
		body.Strings = pool.Strings;
		body.RecordBits = body.PoolStart - headerBits;
		if (body.RecordBits < 0) return null;

		// Decode the records (126-bit each, last possibly truncated) and validate the schema.
		// A record's 7-bit string index = bit 70 (high) << 6 | 6-bit[97..102].
		int pos = headerBits;
		int end = body.PoolStart;
		var indices = new List<int>();
		int match = 0, total = 0;
		while (end - pos >= 104)                 // a record needs at least 104 bits
		{
			int len = Math.Min(126, end - pos);
			int[] bits = new int[len];
			for (int i = 0; i < len; i++)
			{
				int bit = pos + i;
				bits[i] = (data[bit >> 3] >> (7 - (bit & 7))) & 1;
			}
			for (int i = 0; i < len; i++)
			{
				if (IsVariableBit(i)) continue;
				total++;
				if (bits[i] == TemplateBit(i)) match++;
			}
			if (len > 103)
			{
				int b70 = bits[70];
				int b6 = 0;
				for (int k = 97; k < 103; k++) b6 = (b6 << 1) | bits[k];
				indices.Add(b70 << 6 | b6);
			}
			pos += len;
		}
		body.RecordSchemaMatched = total > 0 && (double)match / total >= 0.98;
		// Only expose the record indices when the 1kV schema matched; otherwise they are
		// an artifact of forcing the 1kV record window onto a different schema.
		body.RecordIndices = body.RecordSchemaMatched ? indices.ToArray() : Array.Empty<int>();
		return body;
	}

	static bool IsVariableBit(int bit)
	{
		// bit 70 (index high bit) + bits 97..102 (index low 6 bits) are variable
		if (bit == 70) return true;
		return bit >= 97 && bit <= 102;
	}

	/// <summary>The constant 126-bit record template (bit 70 and bits 97..102 are variable).</summary>
	static int TemplateBit(int bit)
	{
		if (bit >= 0 && bit < 8) return (0x02 >> (7 - bit)) & 1;
		if (bit >= 8 && bit < 16) return (0x94 >> (15 - bit)) & 1;
		if (bit >= 16 && bit < 64) return 0;
		if (bit >= 64 && bit < 72) return (0x04 >> (71 - bit)) & 1;
		if (bit >= 72 && bit < 80) return (0x88 >> (79 - bit)) & 1;
		if (bit >= 80 && bit < 88) return (0x08 >> (87 - bit)) & 1;
		if (bit >= 88 && bit < 96) return (0x0A >> (95 - bit)) & 1;
		if (bit == 96 || bit == 103) return 0;
		if (bit >= 104 && bit < 112) return (0x80 >> (111 - bit)) & 1;
		if (bit >= 112 && bit < 120) return (0xA0 >> (119 - bit)) & 1;
		if (bit >= 120 && bit < 126) return (0b001010 >> (125 - bit)) & 1;
		return 0;
	}

	static bool AllPrintable(string s)
	{
		if (s.Length == 0) return true;
		foreach (char c in s) if (c < 0x20 || c > 0x7E) return false;
		return true;
	}

	/// <summary>A minimal MSB-first bit reader (reads 0 past the end; callers check exactness).</summary>
	struct BitReader
	{
		readonly byte[] _data; readonly int _end; int _bit;
		public BitReader(byte[] data, int start, int end) { _data = data; _bit = start; _end = end; }
		public int Bit => _bit;
		public int End => _end;
		public int Read(int n)
		{
			int v = 0;
			for (int i = 0; i < n; i++)
			{
				int b = (_bit < _end) ? (_data[_bit >> 3] >> (7 - (_bit & 7))) & 1 : 0;
				v = (v << 1) | b;
				_bit++;
			}
			return v;
		}
		public int ReadBitShort()
		{
			int tag = Read(2);
			if (tag == 0) { long u = Read(16); return (int)((u & 0x8000) != 0 ? u - 0x10000L : u); }
			if (tag == 1) return Read(8);
			if (tag == 2) return 0;
			if (tag == 3) return 256;
			throw new InvalidOperationException($"bad ReadBitShort tag {tag}");
		}
	}
}
