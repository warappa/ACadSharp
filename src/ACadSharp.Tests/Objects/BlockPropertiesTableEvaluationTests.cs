using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using CSMath;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace ACadSharp.Tests.Objects;

/// <summary>
/// Verifies the evaluation behaviour of the <see cref="BlockPropertiesTable"/> and
/// <see cref="BlockPropertiesTableGrip"/> classes (the properties table pair, previously
/// data-only stubs). The grip is a user-touchable handle whose value is its stored
/// <c>Location</c>; the table's value is the active row's key string, decoded from the
/// table's body (the 1kV record schema: one 7-bit string-pool index per row; the first
/// record is the active row, since <c>DefaultActiveRowIndex</c> is not yet decoded on
/// disk). See <c>docs/articles/block-properties-table.md</c> and
/// <c>docs/articles/evaluation-engine.md</c>.
/// </summary>
public class BlockPropertiesTableEvaluationTests
{
	//Locate a BPT forensics sample (same pattern as BlockPropertiesTableTests): the
	//BPT_SAMPLE_DIR env var or the repo's samples/bpt-forensics (gitignored research
	//files); the test skips when the file is not present, keeping the suite portable.
	private static string? FindSample(string fileName)
	{
		var env = Environment.GetEnvironmentVariable("BPT_SAMPLE_DIR");
		if (env is not null)
		{
			string p = Path.Combine(env, fileName);
			if (File.Exists(p)) return p;
		}
		var baseDir = AppContext.BaseDirectory;
		for (int i = 0; i < 10; i++)
		{
			string p = Path.GetFullPath(Path.Combine(baseDir, "samples", "bpt-forensics", fileName));
			if (File.Exists(p)) return p;
			string parent = Path.GetFullPath(Path.Combine(baseDir, ".."));
			if (parent == baseDir) break;
			baseDir = parent;
		}
		return null;
	}

	private static (List<BlockPropertiesTable> Tables, List<BlockPropertiesTableGrip> Grips) Collect(CadDocument doc)
	{
		var tables = new List<BlockPropertiesTable>();
		var grips = new List<BlockPropertiesTableGrip>();

		FieldInfo field = typeof(CadDocument).GetField("_cadObjects", BindingFlags.NonPublic | BindingFlags.Instance);
		IDictionary dict = (IDictionary)field.GetValue(doc);
		foreach (DictionaryEntry entry in dict)
		{
			if (entry.Value is BlockPropertiesTable table)
			{
				tables.Add(table);
			}
			else if (entry.Value is BlockPropertiesTableGrip grip)
			{
				grips.Add(grip);
			}
		}

		return (tables, grips);
	}

	/// <summary>
	/// The activation criterion: the parameter grips and the properties-table grip are the
	/// user-touchable handles (the evaluation's activation seeds); the table itself, the
	/// parameters, and the actions are not.
	/// </summary>
	[Fact]
	public void IsActivatableForTheGripsOnly()
	{
		Assert.True(new BlockPropertiesTableGrip().IsActivatable);
		Assert.True(new BlockVisibilityGrip().IsActivatable);
		Assert.False(new BlockPropertiesTable().IsActivatable);
		Assert.False(new BlockLookupAction().IsActivatable);
		Assert.False(new BlockGripLocationComponent().IsActivatable);
	}

	/// <summary>
	/// The table grip is stateful: its default value (before evaluation) is its stored
	/// location, and <c>Evaluate</c> writes it to the "Value" port (a Point).
	/// </summary>
	[Fact]
	public void GripEvaluatesItsStoredLocation()
	{
		XYZ location = new(1.5, -2.5, 0);
		BlockPropertiesTableGrip grip = new() { Id = 42, Location = location };

		//The typed view falls back to the stored location before evaluation.
		Assert.True(grip.CurrentValue.IsSet);
		Assert.Equal(1.5, grip.CurrentValue.Value.X, 9);
		Assert.Equal(-2.5, grip.CurrentValue.Value.Y, 9);
		Assert.Equal(0.0, grip.CurrentValue.Value.Z, 9);

		EvaluationContext context = new();
		Assert.True(grip.Evaluate(context));
		Assert.True(context.TryGetValue(grip.Id, "Value", out EvaluationValue v));
		Assert.Equal(EvaluationValueType.Point, v.Type);
		XYZ p = v.PointValue.Value;
		Assert.Equal(1.5, p.X, 9);
		Assert.Equal(-2.5, p.Y, 9);
		Assert.Equal(0.0, p.Z, 9);
	}

	/// <summary>
	/// A table without a decodable body has no determinable value: the default is unset,
	/// and <c>Evaluate</c> is a no-op (it writes nothing, but succeeds).
	/// </summary>
	[Fact]
	public void TableWithoutBodyHasNoValue()
	{
		BlockPropertiesTable table = new() { Id = 7 };

		Assert.Null(table.ActiveValue);
		Assert.Equal(EvaluationValueType.None, ((EvaluationExpression)table).CurrentValue.Type);
		Assert.False(table.CurrentValue.IsSet);

		EvaluationContext context = new();
		Assert.True(table.Evaluate(context));
		Assert.False(context.HasValue(7, "Value"));
		Assert.False(context.HasValue(7, "Displacement"));
	}

	/// <summary>
	/// A synthetic body matching the 1kV record schema (a 544-bit header, three records
	/// with 7-bit string indices — the last one truncated, like 1kV's 26th record — a
	/// string pool, and a 33-bit tail): the table decodes its records and exposes the
	/// first record's key string (record 0) as its value, written to both the "Value"
	/// and the "Displacement" port.
	/// </summary>
	[Fact]
	public void TableEvaluatesItsFirstRecordKey()
	{
		//30 pool strings; the records reference indices 28, 5, and 0.
		string[] pool = new string[30];
		for (int i = 0; i < 30; i++)
		{
			pool[i] = i == 28 ? "Rechts" : $"S{i}";
		}

		//The bit sequence: 544-bit header (an alternating pattern; the record region must
		//match the 126-bit template) + three records (the last one truncated to 125 bits,
		//like 1kV's 26th record) + a 5-bit zero gap + the pool + a 33-bit tail.
		//The gap is essential: without it, the record-template tail (0b001010) lets the
		//pool scanner (most-strings-wins) pick a false pool start a few bits early.
		var bits = new List<int>();
		void AddBits(int value, int count)
		{
			for (int i = count - 1; i >= 0; i--)
			{
				bits.Add((value >> i) & 1);
			}
		}
		for (int i = 0; i < 544; i++)
		{
			bits.Add(i % 2);
		}
		void AddRecord(int index, int len)
		{
			//The 126-bit record template (BptBodyDecoder, per bit) + the variable 7-bit
			//index (bit 70 = the high bit, bits 97..102 = the low 6 bits).
			for (int i = 0; i < len; i++)
			{
				int bit;
				if (i == 70) bit = (index >> 6) & 1;
				else if (i >= 97 && i <= 102) bit = (index >> (102 - i)) & 1;
				else bit = BptBodyDecoder.TemplateBit126(i);
				bits.Add(bit);
			}
		}
		AddRecord(28, 126);
		AddRecord(5, 126);
		AddRecord(0, 125);
		for (int i = 0; i < 5; i++)
		{
			bits.Add(0);
		}
		foreach (string s in pool)
		{
			//ReadBitShort: tag 01 (8-bit length) + length * 2 UTF-16LE bits.
			bits.Add(0);
			bits.Add(1);
			AddBits(s.Length, 8);
			foreach (char c in s)
			{
				AddBits(c & 0xFF, 8);
				AddBits((c >> 8) & 0xFF, 8);
			}
		}
		for (int i = 0; i < 33; i++)
		{
			bits.Add(0);
		}

		//Pack the bits into bytes (the remaining bits of the last byte are zero).
		int total = bits.Count;
		byte[] raw = new byte[(total + 7) / 8];
		for (int i = 0; i < total; i++)
		{
			if (bits[i] != 0)
			{
				raw[i >> 3] |= (byte)(1 << (7 - (i & 7)));
			}
		}

		BlockPropertiesTable table = new() { Id = 42, RawTail = raw, RawTailBitCount = total };

		//The 1kV schema matched, with the three record indices.
		Assert.Equal(1, table.RecordSchema);
		Assert.Equal(new[] { 28, 5, 0 }, table.RecordIndices);

		//The active row's key (record 0) is the table's value, before and after evaluation.
		Assert.Equal("Rechts", table.ActiveValue);
		Assert.True(table.CurrentValue.IsSet);
		Assert.Equal("Rechts", table.CurrentValue.Value);

		EvaluationContext context = new();
		Assert.True(table.Evaluate(context));
		Assert.True(context.TryGetValue(table.Id, "Value", out EvaluationValue v));
		Assert.Equal(EvaluationValueType.String, v.Type);
		Assert.Equal("Rechts", v.StringValue);
		Assert.True(context.TryGetValue(table.Id, "Displacement", out EvaluationValue d));
		Assert.Equal("Rechts", d.StringValue);
	}

	/// <summary>
	/// The "1kV Keet" block family (one table + one grip in the verified sample): the
	/// table's value is the active row's key (the 26th record's first string, "Rechts"),
	/// and the grip's value is its stored location.
	/// </summary>
	[Fact]
	public void EvaluatesThe1kVSample()
	{
		string sample = FindSample("1kVKeetKOPIE.dwg");
		if (sample is null)
		{
			return;
		}

		CadDocument doc;
		using (var reader = new DwgReader(sample))
		{
			doc = reader.Read();
		}

		var (tables, grips) = Collect(doc);
		Assert.Single(tables);
		Assert.Single(grips);

		BlockPropertiesTable table = tables[0];
		//The active row's key (the first record's string, verified: record 0 = index 28 =
		//"Rechts" in the 126-string pool).
		Assert.Equal("Rechts", table.ActiveValue);
		Assert.True(table.CurrentValue.IsSet);
		Assert.Equal("Rechts", table.CurrentValue.Value);

		EvaluationContext context = new();
		Assert.True(table.Evaluate(context));
		Assert.True(context.TryGetValue(table.Id, "Value", out EvaluationValue v));
		Assert.Equal("Rechts", v.StringValue);
		Assert.True(context.TryGetValue(table.Id, "Displacement", out EvaluationValue d));
		Assert.Equal("Rechts", d.StringValue);

		BlockPropertiesTableGrip grip = grips[0];
		Assert.Equal(grip.Location, grip.CurrentValue.Value);
		Assert.True(grip.Evaluate(context));
		Assert.True(context.TryGetValue(grip.Id, "Value", out EvaluationValue g));
		Assert.Equal(EvaluationValueType.Point, g.Type);
		Assert.Equal(grip.Location, g.PointValue.Value);
	}

	/// <summary>
	/// The "L3-02" block family (three tables + three grips in the verified sample): the
	/// grip's value is its stored location for all three. The tables' values: the
	/// 3536-bit table ("N Spaces") matches the L3-02 96-bit entry schema, which carries
	/// table-metadata entries (not row keys), so its value is undeterminable (unset); the
	/// other two match no schema and are likewise unset.
	/// </summary>
	[Fact]
	public void EvaluatesTheL302Sample()
	{
		string sample = FindSample("L3-02-Dynamic Blocks.dwg");
		if (sample is null)
		{
			return;
		}

		CadDocument doc;
		using (var reader = new DwgReader(sample))
		{
			doc = reader.Read();
		}

		var (tables, grips) = Collect(doc);
		Assert.Equal(3, tables.Count);
		Assert.Equal(3, grips.Count);

		//Every grip's value is its stored location.
		foreach (BlockPropertiesTableGrip grip in grips)
		{
			Assert.Equal(grip.Location, grip.CurrentValue.Value);
			EvaluationContext context = new();
			Assert.True(grip.Evaluate(context));
			Assert.True(context.TryGetValue(grip.Id, "Value", out EvaluationValue v));
			Assert.Equal(EvaluationValueType.Point, v.Type);
			Assert.Equal(grip.Location, v.PointValue.Value);
		}

		//No L3-02 table exposes a row key (the 96-bit entries are metadata; the other two
		//match no schema): the value is unset and Evaluate is a no-op.
		foreach (BlockPropertiesTable table in tables)
		{
			Assert.Null(table.ActiveValue);
			Assert.Equal(EvaluationValueType.None, ((EvaluationExpression)table).CurrentValue.Type);
			EvaluationContext context = new();
			Assert.True(table.Evaluate(context));
			Assert.False(context.HasValue(table.Id, "Value"));
		}
	}
}
