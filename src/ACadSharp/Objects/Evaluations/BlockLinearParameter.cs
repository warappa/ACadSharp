using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;
using CSMath.Extensions;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKLINEARPARAMETER object, used in AutoCAD to control a
/// distance between two points in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockLinearParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockLinearParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockLinearParameter)]
[DxfSubClass(DxfSubclassMarker.BlockLinearParameter)]
public class BlockLinearParameter : Block2PtParameter, IDxfClassDefined
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
	public override string ObjectName => DxfFileToken.ObjectBlockLinearParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockLinearParameter;

	public ParameterValueSet ValueSet { get; set; } = new ParameterValueSet();

	/// <summary>
	/// Evaluates the linear parameter: reads the connected grips' displacements, updates the
	/// base and end points, and computes the value as the signed distance from the base to
	/// the end point along the parameter's axis.
	/// <para>
	/// The value is written to the context under the "Scale" port (the scale factor applied by
	/// the connected action) and the updated point coordinates are written under the
	/// "UpdatedBaseX/Y" and "UpdatedEndX/Y" ports (read by the connected components).
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		this.GetDisplacements(context, out XYZ firstDisp, out XYZ secondDisp);

		XYZ updatedFirst = this.FirstPoint + firstDisp;
		XYZ updatedSecond = this.SecondPoint + secondDisp;

		// The parameter's axis (the initial direction from the base to the end point).
		XYZ axis = this.SecondPoint - this.FirstPoint;
		double axisLength = axis.GetLength();
		if (axisLength > 1e-9)
		{
			axis = axis / axisLength;
		}
		else
		{
			axis = new XYZ(1, 0, 0);
		}

		// The value = the signed distance from the base to the end point along the axis.
		XYZ delta = updatedSecond - updatedFirst;
		double value = delta.Dot(axis);

		context.SetValue(this.Id, "Scale", value);
		context.SetValue(this.Id, "XScale", value);
		context.SetValue(this.Id, "YScale", value);
		this.WriteUpdatedPoints(context, updatedFirst, updatedSecond);
		base.CurrentValue = EvaluationValue.FromDouble(value);

		return true;
	}

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a linear parameter this is the signed distance along the axis.
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockLinearParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockLinearParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}