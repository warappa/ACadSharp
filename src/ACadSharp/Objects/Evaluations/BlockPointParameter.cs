using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKPOINTPARAMETER object, used in AutoCAD to control a
/// position of a point in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockPointParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockPointParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockPointParameter)]
[DxfSubClass(DxfSubclassMarker.BlockPointParameter)]
public class BlockPointParameter : Block1PtParameter, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the description.
	/// </summary>
	[DxfCodeValue(304)]
	public string Description { get; set; }

	/// <summary>
	/// Gets or sets the label text.
	/// </summary>
	[DxfCodeValue(303)]
	public string Label { get; set; }

	/// <summary>
	/// Gets or sets the position of label text.
	/// </summary>
	[DxfCodeValue(1011, 1021, 1031)]
	public XYZ LabelPosition { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockPointParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockPointParameter;

	/// <summary>
	/// Evaluates the point parameter: reads the connected grip's displacement and writes the
	/// X/Y deltas (the "XDelta/YDelta" ports) and the updated location (the "UpdatedX/Y"
	/// ports) into the context.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		this.GetDisplacement(context, out XYZ displacement);

		XYZ updatedLocation = this.Location + displacement;

		context.SetValue(this.Id, "XDelta", displacement.X);
		context.SetValue(this.Id, "YDelta", displacement.Y);
		this.WriteUpdatedLocation(context, updatedLocation);
		this.CurrentValue = displacement.X;

		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockPointParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockPointParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}