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

	/// <summary>
	/// The raw, undecoded DWG payload that follows the <see cref="BeMajor"/>,
	/// <see cref="BeMinor"/> and <see cref="Eed1071"/> fields (the column/row/cell data of
	/// the lookup table). The class has no public ObjectARX API (the data is only reachable
	/// through the .NET/managed wrapper), and the on-disk column/row layout is not yet
	/// decoded (LibreDWG's struct is empty), so the payload is preserved verbatim to keep
	/// DWG round-trips lossless. <see cref="RawTailBitCount"/> is the exact number of
	/// significant bits, which is not guaranteed to be a multiple of 8 (the remaining bits
	/// of the last byte are zero).
	/// </summary>
	public byte[] RawTail { get; set; }

	/// <summary>
	/// The number of significant bits in <see cref="RawTail"/>.
	/// </summary>
	public int RawTailBitCount { get; set; }

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
