using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ACadSharp.Tests.IO.DWG;

/// <summary>
/// Verifies the DWG read of the <see cref="BlockPropertiesTable"/> /
/// <see cref="BlockPropertiesTableGrip"/> classes (the "Block Properties Table" behind a
/// dynamic block's property set) against a real dynamic-blocks file. The on-disk layout was
/// reverse-engineered (see <c>docs/articles/block-properties-table.md</c> and
/// <c>tools/evalgraph-rawdump</c>): the decoded fields are the block-element prefix
/// (<c>BeMajor</c>/<c>BeMinor</c>/<c>Eed1071</c>) and, for the grip,
/// <c>Bl91</c>/<c>Bl92</c>/<c>Location</c>/<c>InsertCycling</c>/<c>InsertCyclingWeight</c>.
/// The undecoded remainder (the table's column/row/cell data, the grip's constant 91-bit gap)
/// is preserved verbatim in <c>RawTail</c> so DWG round-trips stay lossless.
/// </summary>
/// <remarks>
/// These are data-only objects (their <c>Id</c> is *not* part of the evaluation graph), so
/// they are reached by enumerating the document's object table rather than the graph nodes.
/// </remarks>
/// <remarks>
/// The canonical sample is "L3-02-Dynamic Blocks.dwg" (AC1032). It is located via the
/// EVALGRAPH_SAMPLE environment variable (falling back to the local Downloads folder); the
/// test skips when the file is not present so the suite is portable.
/// </remarks>
public class BlockPropertiesTableTests
{
	private readonly ITestOutputHelper _output;

	public BlockPropertiesTableTests(ITestOutputHelper output)
	{
		this._output = output;
	}

	//Locate a BPT forensics sample by file name: the BPT_SAMPLE_DIR env var (if set) or
	//the repo's samples/bpt-forensics (a dedicated, gitignored location). The samples are
	//research files (not the standard dynamic-block test corpus, which DynamicBlockTests
	//scans), so they live outside samples/dynamic-blocks; the test skips when the file is
	//not present, keeping the suite portable.
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

	//The BPT objects are data-only (not part of the evaluation graph), so they are not
	//reached via the graph nodes. Enumerate the document's object table (the private
	//_cadObjects field) and collect them.
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

	[Fact]
	public void ReadDecodesTheTableAndGripFields()
	{
		string sample = FindSample("L3-02-Dynamic Blocks.dwg");
		if (sample is null)
		{
			this._output.WriteLine("SKIP: L3-02 sample not present");
			return;
		}

		CadDocument doc;
		using (var reader = new DwgReader(sample))
		{
			doc = reader.Read();
		}

		var (tables, grips) = Collect(doc);

		this._output.WriteLine($"tables={tables.Count} grips={grips.Count}");

		//The file must yield the objects at all (3 tables + 3 grips in the verified sample).
		Assert.Equal(3, tables.Count);
		Assert.Equal(3, grips.Count);

		//Every object carries the block-element prefix, verified uniform across all six:
		//be_major=33, be_minor=175, eed1071=0.
		foreach (BlockPropertiesTable table in tables)
		{
			Assert.Equal(33, table.BeMajor);
			Assert.Equal(175, table.BeMinor);
			Assert.Equal(0, table.Eed1071);
		}

		foreach (BlockPropertiesTableGrip grip in grips)
		{
			Assert.Equal(33, grip.BeMajor);
			Assert.Equal(175, grip.BeMinor);
			Assert.Equal(0, grip.Eed1071);
			Assert.False(grip.InsertCycling);
			Assert.Equal(-1, grip.InsertCyclingWeight);
		}

		//The per-grip bg_bl91 / bg_bl92 values (the three distinct grips in the sample).
		Assert.Equal(new HashSet<int> { 11, 124, 27 }, grips.Select(g => g.Bl91).ToHashSet());
		Assert.Equal(new HashSet<int> { 12, 125, 28 }, grips.Select(g => g.Bl92).ToHashSet());

		//The grip location (the bg_location 3-D point): in the verified sample one grip has
		//x < 0, one has x > 0, and one has x = 0 (the 2-bit zero sentinel); y and z are
		//"effectively zero" (either the zero sentinel, or a full 64-bit double whose bits
		//decode to a tiny denormal ≈ 1e-14…1e-16). A small tolerance covers both encodings.
		const double eps = 1e-9;
		Assert.All(grips, g => Assert.True(Math.Abs(g.Location.Y) < eps));
		Assert.All(grips, g => Assert.True(Math.Abs(g.Location.Z) < eps));
		Assert.Equal(1, grips.Count(g => Math.Abs(g.Location.X) < eps));
		Assert.Equal(1, grips.Count(g => g.Location.X > 0.0));
		Assert.Equal(1, grips.Count(g => g.Location.X < 0.0));

		//The undecoded remainder is preserved verbatim (lossless round-trip). The three
		//tables carry the column/row/cell data (2596/6533/3536 bits); the three grips carry
		//the constant 91-bit gap (107 bits each, not yet cracked).
		Assert.Equal(new HashSet<int> { 2596, 6533, 3536 }, tables.Select(t => t.RawTailBitCount).ToHashSet());
		Assert.All(tables, t => Assert.True(t.RawTail is { Length: > 0 }));
		Assert.All(grips, g => Assert.Equal(107, g.RawTailBitCount));
		Assert.All(grips, g => Assert.True(g.RawTail is { Length: > 0 }));
	}

	/// <summary>
	/// The "1kV Keet" block family (one table, 31219 bits) matches the full 1kV schema:
	/// a 544-bit header, 26 records (each a 7-bit string-pool index), a 126-string pool,
	/// and a 33-bit tail. Verifies the structured decode end-to-end (see
	/// <c>docs/articles/block-properties-table.md</c> for the bit-exact layout).
	/// </summary>
	[Fact]
	public void ReadDecodesThe1kVBody()
	{
		string sample = FindSample("1kVKeetKOPIE.dwg");
		if (sample is null)
		{
			this._output.WriteLine("SKIP: 1kV sample not present");
			return;
		}

		CadDocument doc;
		using (var reader = new DwgReader(sample))
		{
			doc = reader.Read();
		}

		var (tables, _) = Collect(doc);
		this._output.WriteLine($"tables={tables.Count}");
		Assert.Single(tables);

		BlockPropertiesTable t = tables[0];
		Assert.Equal(31219, t.RawTailBitCount);

		//The full 1kV schema matched.
		Assert.True(t.RecordSchemaMatched);
		Assert.Equal(3798, t.StringPoolStart);

		//The string pool (126 interned UTF-16LE strings) + its verified scaffolding.
		Assert.Equal(126, t.Strings.Length);
		Assert.Contains("Block Table", t.Strings);
		Assert.Contains("Block Table1", t.Strings);
		Assert.Contains("UserVariable", t.Strings);
		Assert.Contains("Custom", t.Strings);

		//The 26 record indices, and the record -> string resolution (a few verified pairs).
		int[] expected = { 28, 29, 53, 118, 119, 94, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 56 };
		Assert.Equal(expected, t.RecordIndices);
		Assert.Equal("Rechts", t.Strings[t.RecordIndices[0]]);
		Assert.Equal("1kV Keet - VPR", t.Strings[t.RecordIndices[1]]);
		Assert.Equal("Klassiek", t.Strings[t.RecordIndices[6]]);
	}

	/// <summary>
	/// The "L3-02" block family (three tables) does NOT match the 1kV record schema, but the
	/// string pool is a uniform mechanism: it still decodes to a clean set of printable
	/// strings (10 / 16 / 21 per table) even though the record region is a different
	/// schema. The 3536-bit table (the "N Spaces" block) additionally matches the L3-02
	/// 96-bit entry schema (a direct label index + an offset-based value index); the other
	/// two (sparser layouts) do not. Verifies the pool decodes for all three, the 1kV
	/// <see cref="BlockPropertiesTable.RecordIndices"/> is empty for all three, and the 96-bit
	/// schema decodes the "N Spaces" block's label/value indices.
	/// </summary>
	[Fact]
	public void ReadDecodesTheStringPoolForOtherBlockFamilies()
	{
		string sample = FindSample("L3-02-Dynamic Blocks.dwg");
		if (sample is null)
		{
			this._output.WriteLine("SKIP: L3-02 sample not present");
			return;
		}

		CadDocument doc;
		using (var reader = new DwgReader(sample))
		{
			doc = reader.Read();
		}

		var (tables, _) = Collect(doc);
		this._output.WriteLine($"tables={tables.Count}");
		Assert.Equal(3, tables.Count);

		//Every table decodes its string pool, and the (bit count -> string count) mapping is
		//the verified one: 2596 -> 10, 6533 -> 16, 3536 -> 21.
		var counts = tables.ToDictionary(t => t.RawTailBitCount, t => t.Strings.Length);
		Assert.Equal(10, counts[2596]);
		Assert.Equal(16, counts[6533]);
		Assert.Equal(21, counts[3536]);

		//The 1kV record schema does not apply to any L3-02 table, so the 1kV record indices
		//are empty for all three.
		Assert.All(tables, t => Assert.Empty(t.RecordIndices));

		//The 3536-bit table (the "N Spaces" block) matches the L3-02 96-bit entry schema:
		//9 full entries (the 10th is truncated, 29 bits, below the 72-bit minimum). The
		//labels are the entry's own pool index (1..9); the values are the offset-based pool
		//index 10 + count (the "N Spaces" group: pool[10] = empty, pool[11] = "1 Space", …).
		BlockPropertiesTable nSpaces = tables.Single(t => t.RawTailBitCount == 3536);
		Assert.Equal(2, nSpaces.RecordSchema);
		Assert.True(nSpaces.RecordSchemaMatched);
		Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, nSpaces.LabelIndices);
		Assert.Equal(new[] { 10, 11, 10, 13, 11, 12, 10, 11, 13 }, nSpaces.ValueIndices);

		//A few verified label/value resolutions (label = a direct pool index, value = 10 + count).
		Assert.Equal("Block Table", nSpaces.Strings[nSpaces.LabelIndices[0]]);
		Assert.Equal("1 Space", nSpaces.Strings[nSpaces.ValueIndices[1]]);
		Assert.Equal("UserVariable", nSpaces.Strings[nSpaces.LabelIndices[4]]);
		Assert.Equal("3 Spaces", nSpaces.Strings[nSpaces.ValueIndices[3]]);

		//The other two L3-02 tables (sparser layouts) do NOT match the 96-bit schema.
		Assert.All(
			tables.Where(t => t.RawTailBitCount != 3536),
			t =>
			{
				Assert.Equal(0, t.RecordSchema);
				Assert.False(t.RecordSchemaMatched);
				Assert.Empty(t.LabelIndices);
				Assert.Empty(t.ValueIndices);
			});
	}
}
