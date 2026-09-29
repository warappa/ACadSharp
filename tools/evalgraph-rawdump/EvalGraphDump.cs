using ACadSharp;
using ACadSharp.Classes;
using ACadSharp.IO;
using ACadSharp.IO.DWG;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Bit-level decoder for the evaluation-graph object classes in a DWG file (R2010+ object section).
//
// Unlike dwg-rawdump (which is hard-coded to a single linear-parameter object), this tool walks
// every object in the objects section and decodes the six evaluation-graph classes:
//   BLOCKPROPERTIESTABLE, BLOCKPROPERTIESTABLEGRIP, BLOCKUSERPARAMETER,
//   BLOCKHORIZONTALCONSTRAINTPARAMETER, BLOCKVERTICALCONSTRAINTPARAMETER, ACDB_DYNAMICBLOCKPROXYNODE.
//
// It uses a self-contained absolute-bit reader (calibrated against dwg-rawdump) and reads the
// object/handle/text regions with the library's header/handle/class readers, so it needs no
// special test scaffolding. It is a read-only forensic tool: it never mutates the file.
class EvalGraphDump
{
	static void Main(string[] args)
	{
		if (args.Length < 1)
		{
			Console.WriteLine("usage: evalgraph-rawdump <path-to-dwg>");
			return;
		}

		string path = args[0];
		var dump = new EvalGraphDump();
		dump.Run(path);
	}

	void Run(string path)
	{
		DwgFileHeader header = new DwgReader(path).readFileHeader();
		Log($"version={header.AcadVersion}");

		byte[] objects = this.ReadSectionBytes(path, header, DwgSectionDefinition.AcDbObjects);
		Log($"objects section: {objects.Length} bytes");

		byte[] handles = this.ReadSectionBytes(path, header, DwgSectionDefinition.Handles);
		IDwgStreamReader hs = DwgStreamReaderBase.GetStreamHandler(header.AcadVersion, new MemoryStream(handles));
		Dictionary<ulong, long> handleMap = new DwgHandleReader(header.AcadVersion, hs).Read();
		Log($"handle map: {handleMap.Count} entries");

		byte[] classes = this.ReadSectionBytes(path, header, DwgSectionDefinition.Classes);
		DxfClassCollection classCollection = new DxfClassCollection(new CadDocument());
		IDwgStreamReader cs = DwgStreamReaderBase.GetStreamHandler(header.AcadVersion, new MemoryStream(classes));
		new DwgClassesReader(header.AcadVersion, cs, header, classCollection).Read();
		Log($"classes: {classCollection.Count}");

		int tableId = classCollection.GetByName("BLOCKPROPERTIESTABLE").ClassNumber;
		int gripId = classCollection.GetByName("BLOCKPROPERTIESTABLEGRIP").ClassNumber;
		int userParamId = classCollection.GetByName("BLOCKUSERPARAMETER").ClassNumber;
		int horizId = classCollection.GetByName("BLOCKHORIZONTALCONSTRAINTPARAMETER").ClassNumber;
		int vertId = classCollection.GetByName("BLOCKVERTICALCONSTRAINTPARAMETER").ClassNumber;
		int proxyId = classCollection.GetByName("ACDB_DYNAMICBLOCKPROXYNODE").ClassNumber;
		Log($"ids: table={tableId} grip={gripId} user={userParamId} horiz={horizId} vert={vertId} proxy={proxyId}");

		Func<double, string> F = d => d.ToString("0.######");

		//Walk every object and decode the target classes bit by bit.
		foreach (long offset in handleMap.Values.OrderBy(o => o))
		{
			BitReader r = new BitReader(objects) { Bit = offset * 8 };
			int size = r.ReadModularShort();
			if (size <= 0)
				continue;
			long handleSizeBits = r.ReadModularChar();
			long headerEnd = r.Bit;
			int classType = r.ReadObjectType();
			if (classType != tableId && classType != gripId && classType != userParamId &&
				classType != horizId && classType != vertId && classType != proxyId)
				continue;

			long dataStart = r.Bit;
			long handleStart = headerEnd + size * 8L - handleSizeBits;
			string name = this.ClassName(classType, tableId, gripId, userParamId, horizId, vertId, proxyId);
			this.DecodeObject(objects, dataStart, handleStart, name, F);
		}
	}

	string ClassName(int classType, int tableId, int gripId, int userParamId, int horizId, int vertId, int proxyId)
	{
		if (classType == tableId) return "TABLE";
		if (classType == gripId) return "GRIP";
		if (classType == userParamId) return "USERPARAM";
		if (classType == horizId) return "HCONSTRAINT";
		if (classType == vertId) return "VCONSTRAINT";
		return "PROXYNODE";
	}

	void DecodeObject(byte[] objects, long dataStart, long handleStart, string name, Func<double, string> F)
	{
		BitReader obj = new BitReader(objects) { Bit = dataStart };
		BitReader handle = new BitReader(objects) { Bit = handleStart };

		//The string stream: flag bit one byte before the handle stream.
		BitReader flagReader = new BitReader(objects) { Bit = handleStart - 1 };
		bool flag = flagReader.ReadBit();
		BitReader text = null;
		if (flag)
		{
			//The 16-bit size is 2 bytes before the flag byte (4 if the 0x8000 bit is set).
			BitReader sizeReader = new BitReader(objects) { Bit = handleStart - 17 };
			int s1 = sizeReader.ReadByte();
			int s2 = sizeReader.ReadByte();
			int sizeShort = s1 | (s2 << 8);
			int total = sizeShort & 0x7FFF;
			long sizePos = handleStart - 17;
			if ((sizeShort & 0x8000) != 0)
			{
				BitReader hiReader = new BitReader(objects) { Bit = handleStart - 33 };
				int h1 = hiReader.ReadByte();
				int h2 = hiReader.ReadByte();
				total |= ((h1 | (h2 << 8)) & 0x7FFF) << 15;
				sizePos = handleStart - 33;
			}
			text = new BitReader(objects) { Bit = sizePos - total };
		}

		Log($"\n=== {name} object: dataStart={dataStart} handleStart={handleStart} textFlag={flag}");
		try
		{
			//readCommonData: own handle + extended data (all from the OBJECT reader).
			ulong ownHandle = obj.HandleReference();
			Log($"  ownHandle={ownHandle}");
			int extSize = obj.ReadBitShort();
			while (extSize != 0)
			{
				ulong appHandle = obj.HandleReference();
				obj.Bit += (long)extSize * 8;
				Log($"    eed appHandle={appHandle} size={extSize}");
				extSize = obj.ReadBitShort();
			}

			//readCommonNonEntityData: owner + reactors + dictionary.
			//owner/reactors/xdic are from the HANDLE reader; counts/flags from the OBJECT reader.
			Log($"  ownerHandle={handle.HandleReference(ownHandle)}");
			int nReactors = obj.ReadBitLong();
			for (int i = 0; i < nReactors; i++)
				Log($"    reactor[{i}]={handle.HandleReference()}");
			bool missing = obj.ReadBit();
			if (!missing)
				Log($"    xDicHandle={handle.HandleReference()}");
			obj.ReadBit(); //R2013+ hasDsBinaryData flag

			//AcDbEvalExpr: Unknown, Value98, Value99, code (+ value), Id
			Log($"  expr Unknown={obj.ReadBitLong()}");
			Log($"  expr Value98={obj.ReadBitLong()}");
			Log($"  expr Value99={obj.ReadBitLong()}");
			int code = obj.ReadBitShort();
			Log($"  expr code={code}");
			if (code > 0)
			{
				var group = GroupCodeValue.TransformValue(code);
				switch (group)
				{
					case GroupCodeValueType.String:
					case GroupCodeValueType.ExtendedDataString:
						Log($"    value(String)={(text == null ? "<none>" : text.ReadVariableText())}");
						break;
					case GroupCodeValueType.Int16:
						Log($"    value(Int16)={obj.ReadBitShort()}");
						break;
					case GroupCodeValueType.Double:
					case GroupCodeValueType.ExtendedDataDouble:
						Log($"    value(Double)={F(obj.ReadBitDouble())}");
						break;
					case GroupCodeValueType.Int32:
					case GroupCodeValueType.Int64:
					case GroupCodeValueType.ExtendedDataInt32:
						Log($"    value(Int32)={obj.ReadBitLong()}");
						break;
					default:
						Log($"    (unhandled group {group})");
						break;
				}
			}
			Log($"  expr Id={obj.ReadBitLong()}");

			Log($"  --- class-specific ---");
			this.DecodeClassSpecific(name, obj, handle, text, handleStart, F);
		}
		catch (Exception ex)
		{
			Log($"  decode stopped at bit {obj.Bit}: {ex.Message}");
		}
	}

	void DecodeClassSpecific(string name, BitReader obj, BitReader handle, BitReader text, long handleStart, Func<double, string> F)
	{
		switch (name)
		{
			case "TABLE":
			{
				int rowCount = obj.ReadBitLong();
				Log($"  RowCount={rowCount}  (bit after = {obj.Bit}, byte-aligned={obj.Bit % 8 == 0}, remaining={handleStart - obj.Bit} bits)");
				this.ProbeTail("  row", obj, handleStart, F);
				this.DumpRemaining(obj, handleStart);
				if (text != null)
					this.DumpTextStream(text);
				break;
			}
			case "GRIP":
			{
				int gripId = obj.ReadBitLong();
				Log($"  GripId={gripId}  (bit after = {obj.Bit}, byte-aligned={obj.Bit % 8 == 0}, remaining={handleStart - obj.Bit} bits)");
				this.ProbeTail("  grip", obj, handleStart, F);
				this.DumpRemaining(obj, handleStart);
				if (text != null)
					this.DumpTextStream(text);
				break;
			}
			case "USERPARAM":
			{
				//readBlockElement: expression (already read) + name (text) + v98/v99/v1071 (object).
				Log($"  element name={(text == null ? "<none>" : text.ReadVariableText())} v98={obj.ReadBitLong()} v99={obj.ReadBitLong()} v1071={obj.ReadBitLong()}");
				//readBlockParameter: show + chain.
				Log($"  parameter show={obj.ReadBit()} chain={obj.ReadBit()}");
				//readBlockUserParameter: value.
				Log($"  value={F(obj.ReadBitDouble())}");
				//Tail (the part the library currently discards): value set.
				this.DecodeValueSet("  user valueSet", obj, F);
				this.DumpRemaining(obj, handleStart);
				break;
			}
			case "HCONSTRAINT":
			case "VCONSTRAINT":
			{
				//readBlock2PtParameter -> readBlockElement: name (text) + v98/v99/v1071 (object).
				Log($"  element name={(text == null ? "<none>" : text.ReadVariableText())} v98={obj.ReadBitLong()} v99={obj.ReadBitLong()} v1071={obj.ReadBitLong()}");
				//readBlockParameter: show + chain.
				Log($"  parameter show={obj.ReadBit()} chain={obj.ReadBit()}");
				//readBlock2PtParameter: first + second (3 doubles each).
				Log($"  first=({F(obj.ReadBitDouble())},{F(obj.ReadBitDouble())},{F(obj.ReadBitDouble())})");
				Log($"  second=({F(obj.ReadBitDouble())},{F(obj.ReadBitDouble())},{F(obj.ReadBitDouble())})");
				//4 displacement properties: count (object) then count x (id object + name text).
				for (int i = 0; i < 4; i++)
				{
					int n = obj.ReadBitShort();
					var parts = new List<string>();
					for (int j = 0; j < n; j++)
					{
						int id = obj.ReadBitLong();
						string dname = text == null ? "<none>" : text.ReadVariableText();
						parts.Add($"{id}:{dname}");
					}
					Log($"  disp[{i}]={n}[{string.Join(",", parts)}]");
				}
				//4 grip ids + base location.
				var grips = new List<int>();
				for (int i = 0; i < 4; i++)
					grips.Add(obj.ReadBitLong());
				int baseLoc = obj.ReadBitShort();
				Log($"  grips=[{string.Join(",", grips)}] baseLoc={baseLoc}");
				//Tail (the part the library currently discards): label, description, labelOffset, valueSet.
				Log($"  label={(text == null ? "<none>" : text.ReadVariableText())}");
				Log($"  description={(text == null ? "<none>" : text.ReadVariableText())}");
				Log($"  labelOffset={F(obj.ReadBitDouble())}");
				this.DecodeValueSet("  valueSet", obj, F);
				this.DumpRemaining(obj, handleStart);
				break;
			}
			case "PROXYNODE":
			{
				this.DumpRemaining(obj, handleStart);
				if (text != null)
					this.DumpTextStream(text);
				break;
			}
		}
	}

	void DecodeValueSet(string label, BitReader obj, Func<double, string> F)
	{
		int vsType = obj.ReadBitLong();
		double vMin = obj.ReadBitDouble();
		double vMax = obj.ReadBitDouble();
		double vInc = obj.ReadBitDouble();
		short vCount = (short)obj.ReadBitShort();
		Log($"{label} type={vsType} min={F(vMin)} max={F(vMax)} inc={F(vInc)} count={vCount}");
		for (int i = 0; i < vCount; i++)
			Log($"  allowed[{i}]={F(obj.ReadBitDouble())}");
	}

	/// <summary>
	/// Tries to decode the tail as a sequence of the most common DWG field types, to help
	/// identify the structure. Re-winds the reader between attempts.
	/// </summary>
	void ProbeTail(string label, BitReader obj, long handleStart, Func<double, string> F)
	{
		long start = obj.Bit;

		//Attempt 1: a run of handle references (the tail may be a list of references).
		obj.Bit = start;
		var handles = new List<ulong>();
		bool ok = true;
		try
		{
			while (obj.Bit < handleStart)
			{
				ulong h = obj.HandleReference();
				handles.Add(h);
				if (handles.Count > 20)
				{
					ok = false;
					break;
				}
			}
		}
		catch
		{
			ok = false;
		}
		Log($"  [probe handles] {(ok ? string.Join(",", handles) : "<failed>")}");

		//Attempt 2: a run of bit-longs.
		obj.Bit = start;
		var longs = new List<int>();
		ok = true;
		try
		{
			while (obj.Bit < handleStart)
			{
				longs.Add(obj.ReadBitLong());
				if (longs.Count > 24)
				{
					ok = false;
					break;
				}
			}
		}
		catch
		{
			ok = false;
		}
		Log($"  [probe longs]   {(ok ? string.Join(",", longs) : "<failed>")}");

		//Attempt 3: a run of bit-doubles.
		obj.Bit = start;
		var doubles = new List<string>();
		ok = true;
		try
		{
			while (obj.Bit < handleStart)
			{
				double d = obj.ReadBitDouble();
				doubles.Add(F(d));
				if (doubles.Count > 16)
				{
					ok = false;
					break;
				}
			}
		}
		catch
		{
			ok = false;
		}
		Log($"  [probe doubles] {(ok ? string.Join(" ", doubles) : "<failed>")}");
	}

	void DumpRemaining(BitReader obj, long handleStart)
	{
		long len = handleStart - obj.Bit;
		if (len <= 0)
		{
			Log("  (no data left)");
			return;
		}
		if (len > 512)
		{
			Log($"  (remaining region is {len} bits = {len / 8} bytes; dumping first 512 bits)");
			len = 512;
		}
		else
		{
			Log($"  (remaining region is {len} bits = {len / 8} bytes)");
		}
		byte[] bytes = new byte[(int)(len + 7) / 8];
		for (int i = 0; i < bytes.Length; i++)
			bytes[i] = (byte)obj.ReadByte();
		this.LogHex(bytes, $"obj+{obj.Bit}");
	}

	void DumpTextStream(BitReader text)
	{
		Log($"  text stream @bit {text.Bit}:");
		for (int i = 0; i < 8; i++)
		{
			string s = text.ReadVariableText();
			Log($"    text[{i}]=\"{s}\"");
			if (s.Length == 0)
				break;
		}
	}

	void LogHex(byte[] data, string tag)
	{
		for (int i = 0; i < data.Length; i += 16)
		{
			int end = Math.Min(data.Length, i + 16);
			string hex = string.Join(" ", data.Skip(i).Take(end - i).Select(b => b.ToString("X2")));
			Log($"    {tag}+{i,3:X4}: {hex}");
		}
	}

	void Log(string message)
	{
		Console.WriteLine(message);
	}

	byte[] ReadSectionBytes(string path, DwgFileHeader header, string sectionName)
	{
		DwgSectionDescriptor descriptor = header.GetDescriptor(sectionName);
		var result = new MemoryStream();
		using (FileStream file = File.OpenRead(path))
		{
			foreach (DwgLocalSectionMap page in descriptor.LocalSections)
			{
				if (page.IsEmpty)
				{
					result.Write(new byte[page.DecompressedSize], 0, (int)page.DecompressedSize);
					continue;
				}

				file.Position = page.Seeker;
				this.DecryptDataSection(page, file);

				if (descriptor.IsCompressed)
				{
					DwgLZ77AC18Decompressor.DecompressToDest(file, result);
				}
				else
				{
					byte[] buffer = new byte[page.CompressedSize];
					file.Read(buffer, 0, buffer.Length);
					result.Write(buffer, 0, buffer.Length);
				}
			}
		}
		return result.ToArray();
	}

	void DecryptDataSection(DwgLocalSectionMap section, Stream sreader)
	{
		int secMask = 0x4164536b ^ (int)sreader.Position;
		for (int i = 0; i < 8; i++)
		{
			byte[] b = new byte[4];
			sreader.Read(b, 0, 4);
			long v = b[0] | (b[1] << 8) | ((long)b[2] << 16) | ((long)b[3] << 24);
			v ^= secMask;
		}
	}

	/// <summary>
	/// A bit reader over an absolute bit position (MSB-first), mirroring the dwg-rawdump tool.
	/// </summary>
	class BitReader
	{
		private readonly byte[] _data;
		public long Bit;
		public int DataLength => this._data.Length;

		public BitReader(byte[] data)
		{
			this._data = data;
		}

		private int RawBit()
		{
			long byteIdx = this.Bit / 8;
			int bitInByte = (int)(this.Bit % 8);
			if (byteIdx >= this._data.Length)
				return 0;
			return (this._data[byteIdx] >> (7 - bitInByte)) & 1;
		}

		private int NBits(int n)
		{
			int v = 0;
			for (int i = 0; i < n; i++)
			{
				v = (v << 1) | this.RawBit();
				this.Bit++;
			}
			return v;
		}

		public int Read2Bits() => this.NBits(2);

		public bool ReadBit() => this.NBits(1) != 0;

		public int ReadByte() => this.NBits(8);

		private int LE(int n)
		{
			int v = 0;
			for (int i = 0; i < n; i++)
			{
				int b = this.NBits(8);
				v |= b << (i * 8);
			}
			return v;
		}

		public int ReadBitLong()
		{
			int tag = this.Read2Bits();
			switch (tag)
			{
				case 0: return this.LE(4);
				case 1: return this.ReadByte();
				case 2: return 0;
				default: throw new Exception($"ReadBitLong tag {tag} @bit {this.Bit}");
			}
		}

		public int ReadBitShort()
		{
			int tag = this.Read2Bits();
			switch (tag)
			{
				case 0: return this.LE(2);
				case 1: return this.ReadByte();
				case 2: return 0;
				case 3: return 256;
				default: throw new Exception($"ReadBitShort tag {tag} @bit {this.Bit}");
			}
		}

		public double ReadBitDouble()
		{
			int tag = this.Read2Bits();
			switch (tag)
			{
				case 0:
				{
					ulong ul = 0;
					for (int i = 0; i < 8; i++)
					{
						int b = this.NBits(8);
						ul |= (ulong)b << (i * 8);
					}
					return BitConverter.Int64BitsToDouble((long)ul);
				}
				case 1: return 1.0;
				case 2: return 0.0;
				default: throw new Exception($"ReadBitDouble tag {tag} @bit {this.Bit}");
			}
		}

		//R2010+ modular short: (b1,b2) byte pairs, 15 payload bits per pair.
		public int ReadModularShort()
		{
			int shift = 0b1111;
			int b1 = this.ReadByte();
			int b2 = this.ReadByte();
			bool flag = (b2 & 0b10000000) == 0;
			int value = b1 | (b2 & 0b1111111) << 8;
			while (!flag)
			{
				b1 = this.ReadByte();
				b2 = this.ReadByte();
				flag = (b2 & 0b10000000) == 0;
				value |= b1 << shift;
				shift += 8;
				value |= (b2 & 0b1111111) << shift;
				shift += 7;
			}
			return value;
		}

		//R2010+ modular char: 7-bit groups while the high bit is set.
		public long ReadModularChar()
		{
			int shift = 0;
			int lastByte = this.ReadByte();
			long value = (long)(lastByte & 0b01111111);
			if ((lastByte & 0b10000000) != 0)
			{
				while (true)
				{
					shift += 7;
					int last = this.ReadByte();
					value |= (long)(last & 0b01111111) << shift;
					if ((last & 0b10000000) == 0)
						break;
				}
			}
			return value;
		}

		//AC24 object type: 2-bit pair + 1 or 2 bytes.
		public int ReadObjectType()
		{
			int pair = this.Read2Bits();
			int value;
			switch (pair)
			{
				case 0: value = this.ReadByte(); break;
				case 1: value = 0x1F0 + this.ReadByte(); break;
				case 2: value = this.LE(2); break;
				case 3: value = this.LE(2); break;
				default: throw new Exception($"ReadObjectType pair {pair}");
			}
			return value;
		}

		//Variable text: length (bit short) then (length << 1) bytes of UTF-16.
		public string ReadVariableText()
		{
			int length = this.ReadBitShort();
			if (length == 0)
				return string.Empty;
			byte[] bytes = new byte[length * 2];
			for (int i = 0; i < bytes.Length; i++)
				bytes[i] = (byte)this.ReadByte();
			return Encoding.Unicode.GetString(bytes).Replace("\0", string.Empty);
		}

		//Handle reference: |CODE (4 bits)|COUNTER (4 bits)|HANDLE/OFFSET|
		public ulong HandleReference(ulong referenceHandle = 0)
		{
			int form = this.ReadByte();
			int code = form >> 4;
			int counter = form & 0b00001111;

			ulong initialPos;
			if (code <= 0x5)
				initialPos = this.ReadHandle(counter);
			else if (code == 0x6)
				initialPos = referenceHandle + 1;
			else if (code == 0x8)
				initialPos = referenceHandle == 0 ? 0 : referenceHandle - 1;
			else if (code == 0xA)
				initialPos = referenceHandle + this.ReadHandle(counter);
			else if (code == 0xC)
				initialPos = referenceHandle == 0 ? 0 : referenceHandle - this.ReadHandle(counter);
			else
				throw new Exception($"HandleReference invalid code {code} @bit {this.Bit}");

			return initialPos;
		}

		private ulong ReadHandle(int length)
		{
			ulong value = 0;
			for (int i = 0; i < length; i++)
			{
				int b = this.ReadByte();
				value |= (ulong)b << (i * 8);
			}
			return value;
		}
	}
}
