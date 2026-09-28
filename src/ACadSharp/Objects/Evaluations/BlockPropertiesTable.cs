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
	/// The number of rows in the table.
	/// </summary>
	[DxfCodeValue(90)]
	public int RowCount { get; set; }

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
