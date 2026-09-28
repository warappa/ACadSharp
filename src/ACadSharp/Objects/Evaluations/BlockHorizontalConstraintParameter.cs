using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKHORIZONTALCONSTRAINTPARAMETER object, used in AutoCAD to constrain a
/// horizontal distance between two points in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockHorizontalConstraintParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockHorizontalConstraintParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockHorizontalConstraintParameter)]
[DxfSubClass(DxfSubclassMarker.BlockHorizontalConstraintParameter)]
public class BlockHorizontalConstraintParameter : Block2PtParameter, IDxfClassDefined
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
	public override string ObjectName => DxfFileToken.ObjectBlockHorizontalConstraintParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockHorizontalConstraintParameter;

	public ParameterValueSet ValueSet { get; set; } = new ParameterValueSet();

	/// <summary>
	/// Evaluates the horizontal constraint parameter: the value is the horizontal (X) distance
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

		// The default horizontal distance (the stored second X minus the stored first X).
		double defaultDistance = this.SecondPoint.X - this.FirstPoint.X;

		// The value = the horizontal (X) distance.
		double value = relative.X;

		// The scale factor = the current distance over the default distance (1 when the
		// parameter is untouched). The default distance can be negative (the end to the left
		// of the base); the signed ratio handles the sign.
		double scale = (defaultDistance > 1e-9 || defaultDistance < -1e-9) ? value / defaultDistance : 1.0;
		context.SetValue(this.Id, "Scale", scale);
		context.SetValue(this.Id, "XScale", scale);
		context.SetValue(this.Id, "YScale", scale);
		base.CurrentValue = EvaluationValue.FromDouble(value);

		return true;
	}

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a horizontal constraint parameter this is the horizontal (X)
	/// distance from the base point to the end point.
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	/// <inheritdoc/>
	protected override EvaluationValue GetDefaultValue() => EvaluationValue.FromDouble(this.SecondPoint.X - this.FirstPoint.X);

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockHorizontalConstraintParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockHorizontalConstraintParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
