using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKBASEPOINTPARAMETER object, used in AutoCAD to control a
/// position of a point in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockBasePointParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockBasePointParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockBasePointParameter)]
[DxfSubClass(DxfSubclassMarker.BlockBasePointParameter)]
public class BlockBasePointParameter : Block1PtParameter, IDxfClassDefined
{
	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockBasePointParameter;

	[DxfCodeValue(1011, 1021, 1031)]
	public XYZ Point1011 { get; set; }

	[DxfCodeValue(1012, 1022, 1032)]
	public XYZ Point1012 { get; set; }

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a base point parameter this is the (static) base point.
	/// </summary>
	public new EvaluationValue<XYZ> CurrentValue => base.CurrentValue.As<XYZ>();

	/// <inheritdoc/>
	protected override EvaluationValue GetDefaultValue() => EvaluationValue.FromPoint(this.Location);

	/// <summary>
	/// Evaluates the base point parameter: a base point is a static reference point (the
	/// block's insertion point), so its value is the stored location (written to the
	/// "Value" port) — it is not updated by connected actions.
	/// <para>
	/// The point never displaces: the displacement ports ("XDelta"/"YDelta") are zero and
	/// the updated location ports ("UpdatedX"/"UpdatedY") equal the stored location.
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		context.SetValue(this.Id, "XDelta", 0.0);
		context.SetValue(this.Id, "YDelta", 0.0);
		context.SetValue(this.Id, "UpdatedX", this.Location.X);
		context.SetValue(this.Id, "UpdatedY", this.Location.Y);
		context.SetValue(this.Id, "Value", EvaluationValue.FromPoint(this.Location));
		base.CurrentValue = EvaluationValue.FromPoint(this.Location);
		return true;
	}

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockBasePointParameter;

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockBasePointParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockBasePointParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
