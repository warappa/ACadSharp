using ACadSharp.Attributes;
using ACadSharp.Classes;

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
	/// The grip id.
	/// </summary>
	[DxfCodeValue(91)]
	public long GripId { get; set; }

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
