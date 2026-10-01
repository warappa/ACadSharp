using ACadSharp;
using ACadSharp.IO;
using ACadSharp.IO.DWG;
using ACadSharp.Objects.Evaluations;
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace ACadSharp.Tools.Bpt;

internal static class Program
{
	static int Main(string[] args)
	{
		if (args.Length < 1)
		{
			Console.WriteLine("usage: bpt-decode <path-to-dwg>");
			return 1;
		}
		string path = args[0];
		CadDocument doc;
		using (var reader = new DwgReader(path)) { doc = reader.Read(); }

		FieldInfo field = typeof(CadDocument).GetField("_cadObjects", BindingFlags.NonPublic | BindingFlags.Instance);
		IDictionary dict = (IDictionary)field.GetValue(doc);
		int idx = 0;
		var sw = Stopwatch.StartNew();
		foreach (DictionaryEntry entry in dict)
		{
			if (entry.Value is BlockPropertiesTable bpt)
			{
				idx++;
				var t = Stopwatch.StartNew();
				BptBodyDecoder.Body body = BptBodyDecoder.Decode(bpt.RawTail, bpt.RawTailBitCount);
				t.Stop();
				Console.WriteLine($"obj{idx}: BPT {bpt.RawTailBitCount} bits, decode {t.ElapsedMilliseconds} ms");
				if (body is null) { Console.WriteLine("  (1kV record schema did not match)"); continue; }
				Console.WriteLine($"  header={body.HeaderBits} records={body.RecordBits} pool={body.PoolBits} tail={body.TailBits} (schema matched={body.RecordSchemaMatched})");
				Console.WriteLine($"  {body.Strings.Length} strings; {body.RecordIndices.Length} record indices = [{string.Join(", ", body.RecordIndices)}]");
				for (int i = 0; i < body.RecordIndices.Length; i++)
				{
					int j = body.RecordIndices[i];
					string s = j < body.Strings.Length ? body.Strings[j] : "?";
					Console.WriteLine($"    k{i} -> [{j}] {s}");
				}
			}
		}
		Console.WriteLine($"total: {idx} BPT objects, {sw.ElapsedMilliseconds} ms");
		return 0;
	}
}
