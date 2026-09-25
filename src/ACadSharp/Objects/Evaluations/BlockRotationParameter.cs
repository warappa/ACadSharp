using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;
using System;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKROTATIONPARAMETER object.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockRotationParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockRotationParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockRotationParameter)]
[DxfSubClass(DxfSubclassMarker.BlockRotationParameter)]
public class BlockRotationParameter : Block2PtParameter, IDxfClassDefined
{
	[DxfCodeValue(306)]
	public string Description { get; set; }

	[DxfCodeValue(305)]
	public string Label { get; set; }

	[DxfCodeValue(140)]
	public double LabelOffset { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockRotationParameter;

	[DxfCodeValue(1011, 1021, 1031)]
	public XYZ Point { get; set; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockRotationParameter;

	public ParameterValueSet ValueSet { get; set; }

	/// <summary>
	/// Evaluates the rotation parameter: reads the connected grips' displacements, updates
	/// the base and end points, and computes the value as the angle from the base to the
	/// end point.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		this.GetDisplacements(context, out XYZ firstDisp, out XYZ secondDisp);

		XYZ updatedFirst = this.FirstPoint + firstDisp;
		XYZ updatedSecond = this.SecondPoint + secondDisp;

		XYZ delta = updatedSecond - updatedFirst;
		double angle = Math.Atan2(delta.Y, delta.X);

		context.SetValue(this.Id, "AngleDelta", angle);
		this.WriteUpdatedPoints(context, updatedFirst, updatedSecond);
		base.CurrentValue = EvaluationValue.FromDouble(angle);

		return true;
	}

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a rotation parameter this is the angle (radians).
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockRotationParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockRotationParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}