using System;
using System.Collections.Generic;
using System.Text;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Decodes a <c>BlockPropertiesTable.RawTail</c> into its on-disk structure (header, records,
/// string pool, tail). Self-contained (no library dependency) so it can be lifted into the
/// library. The methods may return <c>null</c> when the schema does not match; the result
/// classes are nullable-disabled (no <c>?</c>) so the file lifts into the library (which has
/// no nullable context). Two record schemas are supported: the 1kV 126-bit record (a 7-bit
/// index) and the L3-02 96-bit entry (a direct label index + an offset-based value index).
/// See the README for the verified layouts.
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
		public int HeaderBits;                 // 544 (1kV family) or 256 (L3-02)
		public int RecordBits;
		public int PoolStart;                // bit offset of the string pool
		public int PoolBits;
		public int TailBits;                // 33
		public string[] Strings;
		/// <summary>The record schema: 0 = none matched, 1 = the 1kV 126-bit record, 2 = the L3-02 96-bit entry.</summary>
		public int RecordSchema;
		public int[] RecordIndices;          // 7-bit indices (1kV schema)
		/// <summary>The L3-02 96-bit entries' label indices (a direct pool index; empty when not the L3-02 schema).</summary>
		public int[] LabelIndices;
		/// <summary>The L3-02 96-bit entries' value indices (an offset-based pool index = 10 + count; empty when not the L3-02 schema).</summary>
		public int[] ValueIndices;
		public bool RecordSchemaMatched;     // true if any record schema matched
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
	/// Decodes the full BPT body: tries the 1kV schema (544-bit header + 126-bit records, each
	/// with a 7-bit string index) and the L3-02 schema (256-bit header + 96-bit entries, each
	/// with a direct label index + an offset-based value index), plus the shared string pool +
	/// 33-bit tail. Returns the structured body for the matching schema (the pool always decodes;
	/// the record indices are exposed only for the matched schema).
	/// </summary>
	public static Body Decode(byte[] data, int bitCount)
	{
		if (data is null || bitCount <= 0) return null;
		const int tailBits = 33;
		var pool = TryDecodeStringPool(data, bitCount, tailBits);
		if (pool is null) return null;

		// Try the 1kV schema (544-bit header, 126-bit records).
		Body body = Decode126(data, bitCount, 544, pool.Start, pool.BitCount, pool.Strings, tailBits);
		if (body.RecordSchemaMatched)
		{
			body.RecordSchema = 1;
			return body;
		}

		// Try the L3-02 schema (256-bit header, 96-bit entries).
		Body body96 = Decode96(data, bitCount, 256, pool.Start, pool.BitCount, pool.Strings, tailBits);
		if (body96.RecordSchemaMatched)
		{
			body96.RecordSchema = 2;
			return body96;
		}

		// Neither schema matched; return the 1kV body (the pool decodes, the records are empty).
		return body;
	}

	/// <summary>Decodes the 1kV body: 544-bit header + 126-bit records (a 7-bit string index) + pool + 33-bit tail.</summary>
	static Body Decode126(byte[] data, int bitCount, int headerBits, int poolStart, int poolBits, string[] strings, int tailBits)
	{
		var body = new Body
		{
			TotalBits = bitCount,
			HeaderBits = headerBits,
			TailBits = tailBits,
			PoolStart = poolStart,
			PoolBits = poolBits,
			Strings = strings,
			RecordIndices = Array.Empty<int>(),
			LabelIndices = Array.Empty<int>(),
			ValueIndices = Array.Empty<int>(),
		};
		body.RecordBits = poolStart - headerBits;
		if (body.RecordBits < 0) return body;

		// Decode the records (126-bit each, last possibly truncated) and validate the schema.
		// A record's 7-bit string index = bit 70 (high) << 6 | 6-bit[97..102].
		int pos = headerBits;
		int end = poolStart;
		var indices = new List<int>();
		int match = 0, total = 0;
		while (end - pos >= 104)                 // a record needs at least 104 bits
		{
			int len = Math.Min(126, end - pos);
			int[] bits = ReadBits(data, pos, len);
			for (int i = 0; i < len; i++)
			{
				if (IsVariableBit126(i)) continue;
				total++;
				if (bits[i] == TemplateBit126(i)) match++;
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

	/// <summary>Decodes the L3-02 body: 256-bit header + 96-bit entries (a direct label index + an offset-based value index) + pool + 33-bit tail.</summary>
	static Body Decode96(byte[] data, int bitCount, int headerBits, int poolStart, int poolBits, string[] strings, int tailBits)
	{
		var body = new Body
		{
			TotalBits = bitCount,
			HeaderBits = headerBits,
			TailBits = tailBits,
			PoolStart = poolStart,
			PoolBits = poolBits,
			Strings = strings,
			RecordIndices = Array.Empty<int>(),
			LabelIndices = Array.Empty<int>(),
			ValueIndices = Array.Empty<int>(),
		};
		body.RecordBits = poolStart - headerBits;
		if (body.RecordBits < 0) return body;

		// Decode the entries (96-bit each, last possibly truncated) and validate the schema.
		// A 96-bit entry: the label = bits 24..27 (a 4-bit direct pool index), the value =
		// 10 + bits 88..91 (a 4-bit count, an offset-based pool index into the "N Spaces" group).
		int pos = headerBits;
		int end = poolStart;
		var labelIndices = new List<int>();
		var valueIndices = new List<int>();
		int match = 0, total = 0;
		while (end - pos >= 72)                // an entry needs at least 72 bits (to hold label + value)
		{
			int len = Math.Min(96, end - pos);
			int[] bits = ReadBits(data, pos, len);
			for (int i = 0; i < len; i++)
			{
				if (IsVariableBit96(i)) continue;
				total++;
				if (bits[i] == TemplateBit96(i)) match++;
			}
			if (len > 92)                     // a full 96-bit entry has the label (24..27) + value (88..91)
			{
				int labelIndex = 0;
				for (int k = 24; k < 28; k++) labelIndex = (labelIndex << 1) | bits[k];
				int valueCount = 0;
				for (int k = 88; k < 92; k++) valueCount = (valueCount << 1) | bits[k];
				labelIndices.Add(labelIndex);
				valueIndices.Add(10 + valueCount);
			}
			pos += len;
		}
		body.RecordSchemaMatched = total > 0 && (double)match / total >= 0.98;
		// Only expose the label/value indices when the L3-02 schema matched.
		body.LabelIndices = body.RecordSchemaMatched ? labelIndices.ToArray() : Array.Empty<int>();
		body.ValueIndices = body.RecordSchemaMatched ? valueIndices.ToArray() : Array.Empty<int>();
		return body;
	}

	/// <summary>Reads <c>len</c> MSB-first bits starting at <c>bit</c>.</summary>
	static int[] ReadBits(byte[] data, int bit, int len)
	{
		int[] bits = new int[len];
		for (int i = 0; i < len; i++)
		{
			int b = bit + i;
			bits[i] = (data[b >> 3] >> (7 - (b & 7))) & 1;
		}
		return bits;
	}

	static bool IsVariableBit126(int bit)
	{
		// bit 70 (index high bit) + bits 97..102 (index low 6 bits) are variable
		if (bit == 70) return true;
		return bit >= 97 && bit <= 102;
	}

	/// <summary>The constant 126-bit record template (bit 70 and bits 97..102 are variable).</summary>
	static int TemplateBit126(int bit)
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

	static bool IsVariableBit96(int bit)
	{
		// bits 2..9 (byte0 low 6 bits + byte1 high 2 bits), bits 24..27 (byte3 high 4 bits),
		// bits 88..92 (byte11 high 4 bits = the value count + bit 92) are variable
		if (bit >= 2 && bit <= 9) return true;
		if (bit >= 24 && bit <= 27) return true;
		if (bit >= 88 && bit <= 92) return true;
		return false;
	}

	/// <summary>The constant 96-bit entry template (bits 2..9, 24..27, 88..92 are variable).</summary>
	static int TemplateBit96(int bit)
	{
		if (bit >= 0 && bit <= 1) return 0;                              // byte0 top 2 bits = 00
		if (bit >= 10 && bit <= 15) return 0;                           // byte1 low 4 bits = 00
		if (bit >= 16 && bit <= 23) return (0x10 >> (23 - bit)) & 1;    // byte2 = 0x10
		if (bit >= 28 && bit <= 31) return (0x04 >> (31 - bit)) & 1;    // byte3 low 4 bits = 4
		if (bit >= 32 && bit <= 39) return (0x05 >> (39 - bit)) & 1;    // byte4 = 0x05
		if (bit >= 40 && bit <= 47) return (0x28 >> (47 - bit)) & 1;    // byte5 = 0x28
		if (bit >= 48 && bit <= 87) return 0;                           // bytes 6-10 = 00
		if (bit >= 93 && bit <= 95) return 0;                           // byte11 low 3 bits = 000
		return 0;                                                       // bits 2..9, 24..27, 88..92 are variable
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
