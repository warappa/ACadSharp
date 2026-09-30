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

	private string SamplePath =>
		Environment.GetEnvironmentVariable("EVALGRAPH_SAMPLE")
		?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			"Downloads", "L3-02-Dynamic Blocks.dwg");

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
		string sample = this.SamplePath;
		if (!File.Exists(sample))
		{
			this._output.WriteLine($"SKIP: sample not present at {sample}");
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
}
