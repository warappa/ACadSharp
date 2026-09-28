using ACadSharp;
using ACadSharp.IO;
using ACadSharp.IO.DWG;
using ACadSharp.IO.DWG.DwgStreamReaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ACadSharp.Tests.Objects
{
	// TEMPORARY: dumps the raw DWG class data of objects whose classes are not (yet) supported,
	// together with the parsed values of the known classes in the same file, to derive the layouts.
	// Delete this file (and the internal access loosenings) when done.
	public class RawObjectDumpTests
	{
		private readonly ITestOutputHelper _output;

		public RawObjectDumpTests(ITestOutputHelper output)
		{
			this._output = output;
		}

		[Fact]
		public void DumpL302()
		{
			this.DumpFile("/home/warappa/Downloads/L3-02-Dynamic Blocks.dwg",
				new[] { "BLOCKLINEARPARAMETER", "BLOCK2PTPARAMETER", "BLOCKPOLARPARAMETER", "BLOCKROTATIONPARAMETER", "BLOCKALIGNMENTPARAMETER", "BLOCKFLIPPARAMETER", "BLOCKXYPARAMETER", "BLOCKBASEPOINTPARAMETER", "BLOCKUSERPARAMETER", "BLOCKHORIZONTALCONSTRAINTPARAMETER", "BLOCKVERTICALCONSTRAINTPARAMETER", "ACDB_DYNAMICBLOCKPROXYNODE", "BLOCKPROPERTIESTABLE", "BLOCKPROPERTIESTABLEGRIP" });
		}

		[Fact]
		public void DumpPropertiesTable()
		{
			this.DumpFile("/home/warappa/Downloads/Block Properties Table.dwg",
				new[] { "BLOCKPROPERTIESTABLE", "BLOCKPROPERTIESTABLEGRIP", "BLOCKPROPERTIESTABLEENTITY", "BLOCKPROPERTIESTABLEGRIPENTITY", "ACDB_DYNAMICBLOCKPROXYNODE", "BLOCKLINEARPARAMETER" });
		}

		private void DumpFile(string path, string[] targets)
		{
			var cadReader = new DwgReader(path, (s, e) => { });
			CadDocument doc = cadReader.Read();
			var reader = cadReader.ObjectReader;

			// Map: class name -> handles
			var byClass = new Dictionary<string, List<ulong>>();
			foreach (var (handle, offset) in reader._map)
			{
				var type = reader.getEntityType(offset);
				if (!reader._classes.TryGetValue((short)type, out var c))
					continue;
				if (!byClass.TryGetValue(c.DxfName, out var list))
					byClass[c.DxfName] = list = new List<ulong>();
				list.Add(handle);
			}

			foreach (var target in targets)
			{
				if (!byClass.TryGetValue(target, out var handles))
				{
					this._output.WriteLine($"== {target}: not present");
					continue;
				}

				this._output.WriteLine($"== {target}: {handles.Count} object(s)");
				foreach (var handle in handles.Take(3))
				{
					this._output.WriteLine($"--- handle {handle:X}");
					this.DumpObject(doc, reader, handle, target);
				}
			}

			throw new Exception("dump complete; output above");
		}

		private void DumpObject(CadDocument doc, DwgObjectReader reader, ulong handle, string targetClass)
		{
			long offset = reader._map[handle];

			//Object framing: 2-byte size, 1-byte handle size (R2010+), then the data.
			reader._crcReader.Position = offset;
			int size = reader._crcReader.ReadModularShort();
			int handleSize = (int)reader._crcReader.ReadModularChar();
			long dataStart = reader._crcReader.PositionInBits();
			long endBits = dataStart + size * 8 - handleSize;

			//Set up the readers (clones the streams) and read the class number.
			reader.getEntityType(offset);

			//Common header (non-entity, R2013+), exactly as readCommonData + readCommonNonEntityData:
			reader._objectReader.HandleReference();

			//Extended data:
			short edSize = reader._objectReader.ReadBitShort();
			while (edSize != 0)
			{
				reader._objectReader.HandleReference();
				long endPos = reader._objectReader.Position + edSize;
				while (reader._objectReader.Position < endPos)
					reader._objectReader.ReadByte();
				edSize = reader._objectReader.ReadBitShort();
			}

			//Owner:
			reader._handlesReader.HandleReference();

			//Reactors:
			int reactors = reader._objectReader.ReadBitLong();
			for (int i = 0; i < reactors; i++)
				reader._handlesReader.HandleReference();

			//XDictionary:
			bool xdicMissing = reader._objectReader.ReadBit();
			if (!xdicMissing)
				reader._handlesReader.HandleReference();

			//DS binary data flag (R2013+):
			reader._objectReader.ReadBit();

			long classDataStart = reader._objectReader.PositionInBits();
			int dataBits = (int)(endBits - classDataStart);

			//Raw class data bytes, bit-accurate (handles non-byte-aligned starts):
			var ms = reader._memoryStream;
			long savedPos = ms.Position;
			long firstByte = classDataStart / 8;
			long lastByte = (endBits - 1) / 8;
			ms.Position = firstByte;
			var raw = new byte[Math.Max(0, lastByte - firstByte + 1)];
			ms.Read(raw, 0, raw.Length);
			ms.Position = savedPos;

			int skip = (int)(classDataStart % 8);
			byte[] data = new byte[(dataBits + 7) / 8];
			for (int i = 0; i < data.Length; i++)
			{
				int b = 0;
				for (int j = 7; j >= 0; j--)
				{
					int bitIndex = skip + i * 8 + (7 - j);
					int byteIdx = bitIndex / 8;
					int bitInByte = bitIndex % 8;
					if (byteIdx >= 0 && byteIdx < raw.Length)
						b |= ((raw[byteIdx] >> (7 - bitInByte)) & 1) << j;
				}
				data[i] = (byte)b;
			}

			this._output.WriteLine($"  framing: size={size} handleSize={handleSize} classData={dataBits} bits");
			this._output.WriteLine(this.FormatBits(data, classDataStart));

			//Calibration: run the exact reader sequence for the class at the class data start.
			//The merged reader is positioned here (main at classDataStart, text at its region start).
			this.Calibrate(reader, targetClass, classDataStart, endBits);

			//The text section of this object (the text reader is positioned at its start):
			var texts = new List<string>();
			try
			{
				for (int i = 0; i < 12; i++)
				{
					string t = reader._textReader.ReadVariableText();
					if (t.Length > 200 || t.Any(ch => ch < 0x20))
						break;
					texts.Add(t);
				}
			}
			catch (Exception)
			{
				//End of the text section (or an undecodable entry): stop.
			}
			this._output.WriteLine("  texts: " + string.Join(" | ", texts.Select(t => t.Length == 0 ? "<empty>" : $"\"{t}\"")));

			//The parsed values (known classes), for layout calibration:
			if (doc.TryGetCadObject<CadObject>(handle, out var obj))
			{
				this._output.WriteLine($"  parsed: {obj.GetType().Name}");
				foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
				{
					object v;
					try
					{
						v = p.GetValue(obj);
					}
					catch
					{
						continue;
					}
					if (v is CadObject)
						v = v.GetType().Name;
					this._output.WriteLine($"    {p.Name} = {this.Format(v)}");
				}
			}
		}

		private string Format(object v)
		{
			if (v == null)
				return "null";
			if (v is string s)
				return $"\"{s}\"";
			if (v is bool b)
				return b ? "true" : "false";
			if (v is double d)
				return d.ToString("0.######");
			if (v is int i)
				return i.ToString();
			return v.ToString();
		}

		private static readonly HashSet<string> TwoPtDerived = new()
		{
			"BLOCKLINEARPARAMETER", "BLOCKPOLARPARAMETER", "BLOCKROTATIONPARAMETER",
			"BLOCKALIGNMENTPARAMETER", "BLOCKXYPARAMETER", "BLOCK2PTPARAMETER",
			"BLOCKBASEPOINTPARAMETER", "BLOCKHORIZONTALCONSTRAINTPARAMETER", "BLOCKVERTICALCONSTRAINTPARAMETER",
		};

		//Runs the exact reader sequence for the class, printing each field's value and the running bit position.
		//This is the ground truth for the layout (mirrors DwgObjectReader.Objects.cs).
		private void Calibrate(DwgObjectReader reader, string targetClass, long classDataStart, long endBits)
		{
			var m = reader._mergedReaders;
			this._output.WriteLine($"  === calibration @ bit {classDataStart} (end {endBits})");

			try
			{
				//readEvaluationExpression:
				long unknown = m.ReadBitLong();
				long ev98 = m.ReadBitLong();
				long ev99 = m.ReadBitLong();
				short code = m.ReadBitShort();
				long id = m.ReadBitLong();
				this._output.WriteLine($"    [expr] unknown={unknown} v98={ev98} v99={ev99} code={code} id={id}  @ {m.PositionInBits()}");

				//readBlockElement:
				string name = m.ReadVariableText();
				long el98 = m.ReadBitLong();
				long el99 = m.ReadBitLong();
				long el1071 = m.ReadBitLong();
				this._output.WriteLine($"    [element] name={name} v98={el98} v99={el99} v1071={el1071}  @ {m.PositionInBits()}");

				//readBlockParameter:
				bool show = m.ReadBit();
				bool chain = m.ReadBit();
				this._output.WriteLine($"    [parameter] show={show} chain={chain}  @ {m.PositionInBits()}");

				//The 2pt-derived classes have the 2pt base; the user parameter does not.
				if (TwoPtDerived.Contains(targetClass))
				{
					//readBlock2PtParameter base:
					CSMath.XYZ first = m.Read3BitDouble();
					CSMath.XYZ second = m.Read3BitDouble();
					this._output.WriteLine($"    [2pt] first=({first.X},{first.Y},{first.Z}) second=({second.X},{second.Y},{second.Z})  @ {m.PositionInBits()}");

					for (int i = 0; i < 4; i++)
					{
						string s = this.ReadEvalParameterProperty(m);
						this._output.WriteLine($"    [2pt] disp[{i}]={s}  @ {m.PositionInBits()}");
					}

					var grips = new List<long>();
					for (int i = 0; i < 4; i++)
						grips.Add(m.ReadBitLong());
					short baseLoc = m.ReadBitShort();
					this._output.WriteLine($"    [2pt] grips=[{string.Join(",", grips)}] baseLoc={baseLoc}  @ {m.PositionInBits()}");
				}

				//Class-specific tail:
				switch (targetClass)
				{
					case "BLOCKLINEARPARAMETER":
					case "BLOCKHORIZONTALCONSTRAINTPARAMETER":
					case "BLOCKVERTICALCONSTRAINTPARAMETER":
					{
						string label = m.ReadVariableText();
						string desc = m.ReadVariableText();
						double labelOffset = m.ReadBitDouble();
						this._output.WriteLine($"    [tail-linear] label={label} desc={desc} labelOffset={labelOffset}  @ {m.PositionInBits()}");
						this.ReadValueSet(m);
						this._output.WriteLine($"    [tail-extra] start @ {m.PositionInBits()} (end {endBits}, {endBits - m.PositionInBits()} bits remain)");
						try
						{
							double val = m.ReadBitDouble();
							this._output.WriteLine($"      double={val}  @ {m.PositionInBits()}");
							long v1 = m.ReadBitLong();
							this._output.WriteLine($"      long={v1}  @ {m.PositionInBits()}");
							short s1 = m.ReadBitShort();
							this._output.WriteLine($"      short={s1}  @ {m.PositionInBits()}");
							bool b1 = m.ReadBit();
							this._output.WriteLine($"      bit={b1}  @ {m.PositionInBits()}");
						}
						catch (Exception ex2) { this._output.WriteLine($"      [extra failed] {ex2.Message} @ {m.PositionInBits()}"); }
						break;
					}
					case "BLOCKPOLARPARAMETER":
					{
						string label = m.ReadVariableText();
						string desc = m.ReadVariableText();
						string angleName = m.ReadVariableText();
						string angleDesc = m.ReadVariableText();
						double labelOffset = m.ReadBitDouble();
						this._output.WriteLine($"    [polar] label={label} desc={desc} angleName={angleName} angleDesc={angleDesc} labelOffset={labelOffset}  @ {m.PositionInBits()}");
						this.ReadValueSet(m);
						this.ReadValueSet(m);
						break;
					}
					case "BLOCKUSERPARAMETER":
					{
						//Hypothesis: a user parameter has a value + a value set.
						//Probe several field-orderings to find one that decodes cleanly.
						this._output.WriteLine($"    [user] tail start @ {m.PositionInBits()} (end {endBits}, {endBits - m.PositionInBits()} bits remain)");
						try
						{
							//Probe A: value (double) + value set.
							double val = m.ReadBitDouble();
							this._output.WriteLine($"      [A] double={val}  @ {m.PositionInBits()}");
						}
						catch (Exception exA) { this._output.WriteLine($"      [A failed] {exA.Message} @ {m.PositionInBits()}"); }
						break;
					}
					default:
					{
						int remaining = (int)(endBits - m.PositionInBits());
						this._output.WriteLine($"    [tail] {remaining} bits remain after prefix");
						break;
					}
				}
			}
			catch (Exception ex)
			{
				this._output.WriteLine($"    [calibration failed] {ex.GetType().Name}: {ex.Message} (stopped @ {m.PositionInBits()})");
			}
		}

		//readEvalParameterProperty: n=ReadBitShort; n×(ReadBitLong + ReadVariableText).
		private string ReadEvalParameterProperty(IDwgStreamReader m)
		{
			short n = m.ReadBitShort();
			if (n < 0 || n > 16)
				throw new InvalidOperationException($"implausible connection count {n}");
			var parts = new List<string>();
			for (int i = 0; i < n; i++)
			{
				long id = m.ReadBitLong();
				string nm = m.ReadVariableText();
				parts.Add($"{id}:{nm}");
			}
			return $"({n})[{string.Join(",", parts)}]";
		}

		//readParameterValueSet: type=ReadBitLong; min/max/inc=ReadBitDouble; count=ReadBitShort; count×ReadBitDouble.
		private void ReadValueSet(IDwgStreamReader m)
		{
			long type = m.ReadBitLong();
			double min = m.ReadBitDouble();
			double max = m.ReadBitDouble();
			double inc = m.ReadBitDouble();
			short count = m.ReadBitShort();
			if (count < 0 || count > 32)
				throw new InvalidOperationException($"implausible value count {count}");
			var vals = new List<double>();
			for (int i = 0; i < count; i++)
				vals.Add(m.ReadBitDouble());
			this._output.WriteLine($"    [valueSet] type={type} min={min} max={max} inc={inc} count={count} vals=[{string.Join(",", vals)}]  @ {m.PositionInBits()}");
		}

		//A hex dump with bit offsets, 8 bytes per line.
		private string FormatBits(byte[] data, long startBit)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < data.Length; i += 8)
			{
				var parts = new List<string>();
				for (int j = i; j < Math.Min(i + 8, data.Length); j++)
					parts.Add(data[j].ToString("X2"));
				sb.AppendLine($"    [{startBit + i * 8,7}] {string.Join(" ", parts)}");
			}
			return sb.ToString();
		}
	}
}
