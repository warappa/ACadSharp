using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKVERTICALCONSTRAINTPARAMETER object, used in AutoCAD to constrain a
/// vertical distance between two points in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockVerticalConstraintParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockVerticalConstraintParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockVerticalConstraintParameter)]
[DxfSubClass(DxfSubclassMarker.BlockVerticalConstraintParameter)]
public class BlockVerticalConstraintParameter : Block2PtParameter, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the description of the parameter.
	/// </summary>
	[DxfCodeValue(306)]
	public string Description { get; set; }

	/// <summary>
	/// Label text.
	/// </summary>
	[DxfCodeValue(305)]
	public string Label { get; set; }

	/// <summary>
	/// Gets or sets the label offset.
	/// </summary>
	[DxfCodeValue(140)]
	public double LabelOffset { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockVerticalConstraintParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockVerticalConstraintParameter;

	public ParameterValueSet ValueSet { get; set; } = new ParameterValueSet();

	/// <summary>
	/// Evaluates the vertical constraint parameter: the value is the vertical (Y) distance
	/// from the (updated) base point to the (updated) end point.
	/// <para>
	/// The value is written to the context under the "Scale" port (the scale factor applied by
	/// the connected action) and the updated point coordinates are written under the
	/// "UpdatedBaseX/Y" and "UpdatedEndX/Y" ports (read by the connected components).
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		this.WritePorts(context);

		// The relative vector from the (updated) base point to the (updated) end point.
		XYZ relative = this.GetRelativeVector(context);

		// The default vertical distance (the stored second Y minus the stored first Y).
		double defaultDistance = this.SecondPoint.Y - this.FirstPoint.Y;

		// The value = the vertical (Y) distance.
		double value = relative.Y;

		// The scale factor = the current distance over the default distance (1 when the
		// parameter is untouched). The default distance can be negative (the end below the
		// base); the signed ratio handles the sign.
		double scale = (defaultDistance > 1e-9 || defaultDistance < -1e-9) ? value / defaultDistance : 1.0;
		context.SetValue(this.Id, "Scale", scale);
		context.SetValue(this.Id, "XScale", scale);
		context.SetValue(this.Id, "YScale", scale);
		base.CurrentValue = EvaluationValue.FromDouble(value);

		return true;
	}

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a vertical constraint parameter this is the vertical (Y)
	/// distance from the base point to the end point.
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockVerticalConstraintParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockVerticalConstraintParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
