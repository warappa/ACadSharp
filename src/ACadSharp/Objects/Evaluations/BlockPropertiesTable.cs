using System;
using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKPROPERTIESTABLE object, a data object that supports the dynamic block
/// properties table (a UI feature for displaying block properties).
/// <para>
/// During evaluation the table exposes the key string of its active row (the table's current
/// value, see <see cref="ActiveValue"/>) and writes it to the "Value" and "Displacement"
/// output ports for the parameters the table feeds (see <see cref="Evaluate"/>).
/// </para>
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
	private EvaluationValue _activeValue = EvaluationValue.None;
	private bool _activeValueComputed;

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
		set { _rawTail = value; _bodyComputed = false; _body = null; _activeValue = EvaluationValue.None; _activeValueComputed = false; }
	}

	/// <summary>
	/// The number of significant bits in <see cref="RawTail"/>.
	/// </summary>
	public int RawTailBitCount
	{
		get { return _rawTailBitCount; }
		set { _rawTailBitCount = value; _bodyComputed = false; _body = null; _activeValue = EvaluationValue.None; _activeValueComputed = false; }
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

	/// <summary>
	/// The matched record schema: 0 = none, 1 = the 1kV 126-bit record (a 7-bit string index),
	/// 2 = the L3-02 96-bit entry (a direct label index + an offset-based value index).
	/// </summary>
	public int RecordSchema
	{
		get
		{
			BptBodyDecoder.Body body = GetBody();
			return body is null ? 0 : body.RecordSchema;
		}
	}

	/// <summary>
	/// The L3-02 96-bit entries' label indices (a direct string-pool index; empty when the
	/// schema is not the L3-02 96-bit entry). Each label references the entry's own pool slot
	/// (the "label" string, e.g. <c>Block Table</c>, <c>UserVariable</c>, or empty).
	/// </summary>
	public int[] LabelIndices
	{
		get
		{
			BptBodyDecoder.Body body = GetBody();
			return body is null ? Array.Empty<int>() : body.LabelIndices;
		}
	}

	/// <summary>
	/// The L3-02 96-bit entries' value indices (an offset-based string-pool index = 10 + count;
	/// empty when the schema is not the L3-02 96-bit entry). The record stores a small count
	/// and the pool index is derived (the group base 10 is implicit in the block's "value"
	/// type, e.g. the "N Spaces" group).
	/// </summary>
	public int[] ValueIndices
	{
		get
		{
			BptBodyDecoder.Body body = GetBody();
			return body is null ? Array.Empty<int>() : body.ValueIndices;
		}
	}

	/// <summary>
	/// The key string of the table's active row (the table's current value), or <c>null</c>
	/// when it cannot be determined from the decoded body.
	/// <para>
	/// A record of the 1kV record schema (a 126-bit record holding a 7-bit index into the
	/// string pool) references the row's key string (a 1kV block: the Dutch state name, e.g.
	/// <c>Rechts</c> or <c>Dubbelzijdig</c>). The on-disk location of
	/// <c>DefaultActiveRowIndex</c> is not yet decoded, so the engine exposes the first
	/// record's key as the table's current value (preliminary: the first row is treated as
	/// the active row).
	/// </para>
	/// <para>
	/// The L3-02 96-bit entry is a table-metadata entry (a name-group + value-group pair),
	/// not a row: no active-row value can be determined from it, so <c>null</c> is returned
	/// for the L3-02 schema.
	/// </para>
	/// </summary>
	public string ActiveValue
	{
		get
		{
			EvaluationValue v = this.GetActiveValue();
			return v.Type == EvaluationValueType.String ? v.StringValue : null;
		}
	}

	private BptBodyDecoder.Body GetBody()
	{
		if (_bodyComputed) return _body;
		_body = (_rawTail is null || _rawTailBitCount <= 0) ? null : BptBodyDecoder.Decode(_rawTail, _rawTailBitCount);
		_bodyComputed = true;
		return _body;
	}

	/// <summary>
	/// The table's current value as an <see cref="EvaluationValue"/> (the active row's key
	/// string, or <see cref="EvaluationValue.None"/> when it cannot be determined).
	/// </summary>
	private EvaluationValue GetActiveValue()
	{
		if (_activeValueComputed)
		{
			return _activeValue;
		}

		_activeValue = EvaluationValue.None;
		BptBodyDecoder.Body body = this.GetBody();
		if (body is not null && body.RecordSchema == 1)
		{
			int[] indices = body.RecordIndices;
			if (indices.Length > 0)
			{
				int index = indices[0];
				if (index >= 0 && index < body.Strings.Length)
				{
					string key = body.Strings[index];
					if (!string.IsNullOrEmpty(key))
					{
						_activeValue = EvaluationValue.FromString(key);
					}
				}
			}
		}
		_activeValueComputed = true;
		return _activeValue;
	}

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockPropertiesTable;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockPropertiesTable;

	/// <summary>
	/// The default value of the table: the active row's key string (the table's current
	/// value, see <see cref="ActiveValue"/>), or <see cref="EvaluationValue.None"/> when it
	/// cannot be determined from the decoded body.
	/// <para>
	/// The body holds the table's state (one record per row, referencing the string pool):
	/// it carries a meaningful value even before the node is evaluated, unlike a pure
	/// computation action (for example a scale), whose value exists only after evaluation.
	/// </para>
	/// </summary>
	protected override EvaluationValue GetDefaultValue() => this.GetActiveValue();

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a table this is the active row's key string.
	/// <para>
	/// Falls back to the <see cref="ActiveValue"/> when the node has not been evaluated yet (the
	/// evaluation result is unset), so the table always exposes its current value rather than
	/// an empty placeholder (or the unset value when the body did not decode).
	/// </para>
	/// </summary>
	public new EvaluationValue<string> CurrentValue => base.CurrentValue.As<string>();

	/// <summary>
	/// Evaluates the table: writes the active row's key string (the table's current value,
	/// see <see cref="ActiveValue"/>) to the "Value" output port — and, because the table's
	/// parameter bindings are not decoded (the graph stores no per-parameter connection
	/// name), to the "Displacement" port as well (the fallback port name of the linear
	/// parameter the 1kV table feeds, so the value is visible on that edge too).
	/// <para>
	/// When the active-row value cannot be determined (the body did not decode, or the
	/// schema carries no row key — for example the L3-02 metadata entries), the node writes
	/// nothing (a no-op evaluation, like the base implementation).
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		EvaluationValue value = this.GetActiveValue();
		if (value.Type == EvaluationValueType.String)
		{
			context.SetValue(this.Id, "Value", value);
			context.SetValue(this.Id, "Displacement", value);
			base.CurrentValue = value;
		}
		return true;
	}

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
