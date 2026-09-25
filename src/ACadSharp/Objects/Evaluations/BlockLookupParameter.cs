using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKLOOKUPPARAMETER object, used in AutoCAD to drive block geometry from
/// a lookup table in the Block Properties Table (the "Lookup" rows of the Customize tab of
/// a dynamic block).
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockLookupParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockLookupParameter"/> <br/>
/// <br/>
/// Minimal implementation: only the common block-element prefix (which carries
/// <see cref="EvaluationExpression.Id"/>, code 90 — the key found in ACAD_ENHANCEDBLOCKDATA)
/// and the display name are decoded. The lookup table itself is not decoded.
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockLookupParameter)]
[DxfSubClass(DxfSubclassMarker.BlockLookupParameter)]
public class BlockLookupParameter : Block1PtParameter, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the action ID associated with the block lookup parameter. This ID is used to link the parameter to a specific action in the dynamic block's evaluation graph.
	/// </summary>
	[DxfCodeValue(94)]
	public int ActionId { get; set; }

	/// <summary>
	/// Gets or sets the description of the block lookup parameter. This description is used to provide additional information about the parameter in the Block Properties Table and in the Customize tab of a dynamic block.
	/// </summary>
	[DxfCodeValue(304)]
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the label of the block lookup parameter. This label is used to identify the parameter in the Block Properties Table and in the Customize tab of a dynamic block.
	/// </summary>
	[DxfCodeValue(303)]
	public string Label { get; set; } = string.Empty;

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockLookupParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockLookupParameter;

	/// <summary>
	/// Evaluates the lookup parameter: reads the connected grip's displacement and writes
	/// the updated location (the "UpdatedX/Y" ports) into the context.
	/// <para>
	/// The lookup table itself is not decoded, so the table-driven value selection is not
	/// implemented; the evaluation only propagates the grip's displacement.
	/// </para>
	/// </summary>
	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a lookup parameter this is the full (X, Y) displacement.
	/// </summary>
	public new EvaluationValue<XYZ> CurrentValue => base.CurrentValue.As<XYZ>();

	public override bool Evaluate(EvaluationContext context)
	{
		this.GetDisplacement(context, out XYZ displacement);

		XYZ updatedLocation = this.Location + displacement;

		this.WriteUpdatedLocation(context, updatedLocation);
		base.CurrentValue = EvaluationValue.FromPoint(displacement);

		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockLookupParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockLookupParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}