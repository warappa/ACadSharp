using System;
using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKPROPERTIESTABLE object, a data object that supports the dynamic block
/// properties table (a UI feature for displaying block properties).
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockPropertiesTable"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockPropertiesTable"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockPropertiesTable)]
[DxfSubClass(DxfSubclassMarker.BlockPropertiesTable)]
public class BlockPropertiesTable : EvaluationExpression, IDxfClassDefined
{
	/// <summary>
	/// The block-element major version (the <c>be_major</c> field; 33 in the verified
	/// R2010+ samples). The first class-specific field after the <c>AcDbEvalExpr</c>
	/// expression.
	/// </summary>
	[DxfCodeValue(90)]
	public int BeMajor { get; set; }

	/// <summary>
	/// The block-element minor version (the <c>be_minor</c> field; 175 in the verified
	/// R2010+ samples).
	/// </summary>
	[DxfCodeValue(91)]
	public int BeMinor { get; set; }

	/// <summary>
	/// The block-element extended-data flag (the <c>eed1071</c> field; 0 in the verified
	/// R2010+ samples).
	/// </summary>
	[DxfCodeValue(92)]
	public int Eed1071 { get; set; }

	private byte[] _rawTail;
	private int _rawTailBitCount;
	private BptBodyDecoder.Body _body;
	private bool _bodyComputed;

	/// <summary>
	/// The raw DWG payload that follows the <see cref="BeMajor"/>, <see cref="BeMinor"/>
	/// and <see cref="Eed1071"/> fields (the column/row/cell data of the lookup table). The
	/// class has no public ObjectARX API, so the payload is preserved verbatim to keep DWG
	/// round-trips lossless. It is a bit-packed, string-interning table: a header, a set of
	/// records (each holding a 7-bit index), a <see cref="Strings"/> pool, and a tail. The
	/// decoded form is reachable through <see cref="Strings"/> / <see cref="RecordIndices"/>
	/// (see <see cref="BptBodyDecoder"/>). <see cref="RawTailBitCount"/> is the exact number
	/// of significant bits, which is not guaranteed to be a multiple of 8 (the remaining
	/// bits of the last byte are zero).
	/// </summary>
	public byte[] RawTail
	{
		get { return _rawTail; }
		set { _rawTail = value; _bodyComputed = false; _body = null; }
	}

	/// <summary>
	/// The number of significant bits in <see cref="RawTail"/>.
	/// </summary>
	public int RawTailBitCount
	{
		get { return _rawTailBitCount; }
		set { _rawTailBitCount = value; _bodyComputed = false; _body = null; }
	}

	/// <summary>
	/// The decoded string pool of the lookup table: the interned UTF-16LE strings that the
	/// table's records reference (decoded from <see cref="RawTail"/> by
	/// <see cref="BptBodyDecoder"/>). Empty when the body did not decode (for example a
	/// block type that stores its strings inline rather than in a trailing pool).
	/// </summary>
	public string[] Strings
	{
		get
		{
			BptBodyDecoder.Body body = GetBody();
			return body is null ? Array.Empty<string>() : body.Strings;
		}
	}

	/// <summary>
	/// The 7-bit string-pool index of each table record (<c>bit 70 &lt;&lt; 6 | 6-bit[97..102]</c>),
	/// for the 1kV block family. Empty when the 1kV record schema did not match (see
	/// <see cref="RecordSchemaMatched"/>).
	/// </summary>
	public int[] RecordIndices
	{
		get
		{
			BptBodyDecoder.Body body = GetBody();
			return body is null ? Array.Empty<int>() : body.RecordIndices;
		}
	}

	/// <summary>
	/// The bit offset, within <see cref="RawTail"/>, at which the string pool starts, or
	/// <c>-1</c> when the body did not decode.
	/// </summary>
	public int StringPoolStart
	{
		get
		{
			BptBodyDecoder.Body body = GetBody();
			return body is null ? -1 : body.PoolStart;
		}
	}

	/// <summary>
	/// Whether the 1kV record schema (a 544-bit header followed by 126-bit records) matched
	/// the body. The string pool itself is a uniform mechanism; the record schema that
	/// references it is block-family-specific.
	/// </summary>
	public bool RecordSchemaMatched
	{
		get
		{
			BptBodyDecoder.Body body = GetBody();
			return body is not null && body.RecordSchemaMatched;
		}
	}

	private BptBodyDecoder.Body GetBody()
	{
		if (_bodyComputed) return _body;
		_body = (_rawTail is null || _rawTailBitCount <= 0) ? null : BptBodyDecoder.Decode(_rawTail, _rawTailBitCount);
		_bodyComputed = true;
		return _body;
	}

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockPropertiesTable;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockPropertiesTable;

	/// <summary>
	/// The properties table is a data-only stub (the lookup table is not decoded), so it has
	/// no meaningful value before evaluation.
	/// </summary>
	protected override EvaluationValue GetDefaultValue() => EvaluationValue.None;

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockPropertiesTable,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockPropertiesTable,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
