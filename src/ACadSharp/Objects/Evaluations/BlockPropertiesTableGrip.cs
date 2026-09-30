using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKPROPERTIESTABLEGRIP object, a data object that supports the dynamic
/// block properties table (a UI feature for displaying block properties).
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockPropertiesTableGrip"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockPropertiesTableGrip"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockPropertiesTableGrip)]
[DxfSubClass(DxfSubclassMarker.BlockPropertiesTableGrip)]
public class BlockPropertiesTableGrip : EvaluationExpression, IDxfClassDefined
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

	/// <summary>
	/// The grip's <c>bg_bl91</c> field (11 in the verified R2010+ samples).
	/// </summary>
	[DxfCodeValue(93)]
	public int Bl91 { get; set; }

	/// <summary>
	/// The grip's <c>bg_bl92</c> field (12 in the verified R2010+ samples).
	/// </summary>
	[DxfCodeValue(94)]
	public int Bl92 { get; set; }

	/// <summary>
	/// The grip location (the <c>bg_location</c> 3-D point; <c>x</c> is the only component
	/// written as a full double in the verified R2010+ samples, <c>y</c>/<c>z</c> are the
	/// zero sentinel).
	/// </summary>
	[DxfCodeValue(10, 20, 30)]
	public XYZ Location { get; set; } = XYZ.Zero;

	/// <summary>
	/// The <c>bg_insert_cycling</c> flag (0 in the verified R2010+ samples).
	/// </summary>
	[DxfCodeValue(95)]
	public bool InsertCycling { get; set; }

	/// <summary>
	/// The <c>bg_insert_cycling_weight</c> field (the 0x8000-bit sentinel = −1 in the
	/// verified R2010+ samples).
	/// </summary>
	[DxfCodeValue(96)]
	public long InsertCyclingWeight { get; set; }

	/// <summary>
	/// The raw, undecoded DWG payload that follows the decoded fields above (the constant
	/// 91-bit field — most likely the <c>name</c> <c>T</c> field's main-stream length plus
	/// a fixed payload — that is not yet cracked). It is preserved verbatim to keep DWG
	/// round-trips lossless. <see cref="RawTailBitCount"/> is the exact number of
	/// significant bits, which is not guaranteed to be a multiple of 8 (the remaining bits
	/// of the last byte are zero).
	/// </summary>
	public byte[] RawTail { get; set; }

	/// <summary>
	/// The number of significant bits in <see cref="RawTail"/>.
	/// </summary>
	public int RawTailBitCount { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockPropertiesTableGrip;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockPropertiesTableGrip;

	/// <summary>
	/// The properties table grip is a data-only stub (the lookup table is not decoded), so it
	/// has no meaningful value before evaluation.
	/// </summary>
	protected override EvaluationValue GetDefaultValue() => EvaluationValue.None;

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockPropertiesTableGrip,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockPropertiesTableGrip,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
