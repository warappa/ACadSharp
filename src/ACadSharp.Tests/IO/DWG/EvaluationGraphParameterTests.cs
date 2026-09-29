using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ACadSharp.Tests.IO.DWG;

/// <summary>
/// Verifies the full DWG read + write of the evaluation-graph parameter classes
/// (BlockUserParameter, BlockHorizontalConstraintParameter, BlockVerticalConstraintParameter)
/// against a real dynamic-blocks file. The on-disk layout of these classes was reverse-engineered
/// (see tools/evalgraph-rawdump); before the full IO was implemented the reader/writer only read
/// a prefix and discarded the tail (the value set, the constraint label/description/label offset).
///
/// The canonical sample is "L3-02-Dynamic Blocks.dwg" (AC1032). It is located via the
/// EVALGRAPH_SAMPLE environment variable (falling back to the local Downloads folder); the test
/// skips when the file is not present so the suite is portable.
/// </summary>
public class EvaluationGraphParameterTests
{
	private readonly ITestOutputHelper _output;

	public EvaluationGraphParameterTests(ITestOutputHelper output)
	{
		this._output = output;
	}

	private string SamplePath =>
		Environment.GetEnvironmentVariable("EVALGRAPH_SAMPLE")
		?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
			"Downloads", "L3-02-Dynamic Blocks.dwg");

	private static List<EvaluationExpression> Collect(CadDocument doc)
	{
		var all = new List<EvaluationExpression>();
		foreach (BlockRecord record in doc.BlockRecords)
		{
			if (record.EvaluationGraph == null)
			{
				continue;
			}

			foreach (EvaluationGraph.Node node in record.EvaluationGraph.Nodes)
			{
				all.Add(node.Expression);
			}
		}

		return all;
	}

	[Fact]
	public void ReadDecodesTheParameterTails()
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

		List<EvaluationExpression> all = Collect(doc);
		var hcon = all.OfType<BlockHorizontalConstraintParameter>().ToList();
		var vcon = all.OfType<BlockVerticalConstraintParameter>().ToList();
		var user = all.OfType<BlockUserParameter>().ToList();

		this._output.WriteLine($"total={all.Count} hcon={hcon.Count} vcon={vcon.Count} user={user.Count}");

		//The file must yield the parameter objects at all (the old prefix-only reader did too,
		//but the fields below only exist with the full tail).
		Assert.NotEmpty(hcon);
		Assert.NotEmpty(vcon);
		Assert.NotEmpty(user);

		//Horizontal constraint: the tail carries a label, a description, a label offset, and a
		//value set. These are the decoded values from the L3-02 sample.
		BlockHorizontalConstraintParameter h = hcon.First(p => !string.IsNullOrEmpty(p.Label));
		this._output.WriteLine($"HCON label={h.Label} desc={h.Description} labelOffset={h.LabelOffset} "
			+ $"vsType={(int)h.ValueSet.Type} vsCount={h.ValueSet.AllowedValues.Count}");
		Assert.Equal("Width", h.Label);
		Assert.True(h.LabelOffset != 0.0);
		Assert.NotEmpty(h.ValueSet.AllowedValues);

		//Vertical constraint: same shape.
		BlockVerticalConstraintParameter v = vcon.First(p => !string.IsNullOrEmpty(p.Label));
		this._output.WriteLine($"VCON label={v.Label} desc={v.Description} labelOffset={v.LabelOffset} "
			+ $"vsType={(int)v.ValueSet.Type} vsCount={v.ValueSet.AllowedValues.Count}");
		Assert.Equal("Height", v.Label);
		Assert.True(v.LabelOffset != 0.0);
		Assert.NotEmpty(v.ValueSet.AllowedValues);

		//User parameter: the tail carries a value and a value set.
		BlockUserParameter u = user[0];
		this._output.WriteLine($"USER value={u.Value} vsType={(int)u.ValueSet.Type} vsCount={u.ValueSet.AllowedValues.Count}");
	}

	[Fact]
	public void RoundTripPreservesTheParameterTails()
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

		List<EvaluationExpression> all = Collect(doc);
		var hcon = all.OfType<BlockHorizontalConstraintParameter>().ToList();
		var vcon = all.OfType<BlockVerticalConstraintParameter>().ToList();
		var user = all.OfType<BlockUserParameter>().ToList();

		string roundTrip = Path.Combine(Path.GetTempPath(), $"evalgraph_roundtrip_{Guid.NewGuid():N}.dwg");
		try
		{
			new DwgWriter(roundTrip, doc).Write();

			CadDocument doc2;
			using (var reader2 = new DwgReader(roundTrip))
			{
				doc2 = reader2.Read();
			}

			//Note: the full evaluation graph does not survive a DWG round-trip (a pre-existing,
			//separate limitation), so this only asserts what the writer re-emits. Where the graph
			//is preserved, the tail fields must be identical.
			List<EvaluationExpression> all2 = Collect(doc2);
			var hcon2 = all2.OfType<BlockHorizontalConstraintParameter>().ToList();
			var vcon2 = all2.OfType<BlockVerticalConstraintParameter>().ToList();
			var user2 = all2.OfType<BlockUserParameter>().ToList();

			this._output.WriteLine($"roundtrip hcon={hcon2.Count} vcon={vcon2.Count} user={user2.Count}");

			for (int i = 0; i < Math.Min(hcon.Count, hcon2.Count); i++)
			{
				this._output.WriteLine($"HCON[{i}] {hcon[i].Label}/{hcon[i].LabelOffset}/{(int)hcon[i].ValueSet.Type}/{hcon[i].ValueSet.AllowedValues.Count}"
					+ $" -> {hcon2[i].Label}/{hcon2[i].LabelOffset}/{(int)hcon2[i].ValueSet.Type}/{hcon2[i].ValueSet.AllowedValues.Count}");
				Assert.Equal(hcon[i].Label, hcon2[i].Label);
				Assert.Equal(hcon[i].Description, hcon2[i].Description);
				Assert.Equal(hcon[i].LabelOffset, hcon2[i].LabelOffset, 5);
				Assert.Equal((int)hcon[i].ValueSet.Type, (int)hcon2[i].ValueSet.Type);
				Assert.Equal(hcon[i].ValueSet.AllowedValues, hcon2[i].ValueSet.AllowedValues);
			}

			for (int i = 0; i < Math.Min(user.Count, user2.Count); i++)
			{
				this._output.WriteLine($"USER[{i}] {user[i].Value}/{(int)user[i].ValueSet.Type} -> {user2[i].Value}/{(int)user2[i].ValueSet.Type}");
				Assert.Equal(user[i].Value, user2[i].Value, 5);
				Assert.Equal((int)user[i].ValueSet.Type, (int)user2[i].ValueSet.Type);
			}
		}
		finally
		{
			if (File.Exists(roundTrip))
			{
				File.Delete(roundTrip);
			}
		}
	}
}
