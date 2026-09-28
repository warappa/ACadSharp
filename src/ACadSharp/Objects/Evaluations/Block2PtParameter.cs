using ACadSharp.Attributes;
using CSMath;
using System;
using System.Collections.Generic;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents the dynamic block 2 point parameter.
/// </summary>
[DxfSubClass(DxfSubclassMarker.Block2PtParameter)]
public abstract class Block2PtParameter : BlockParameter
{
	/// <summary>
	/// Base point the distance is measured from (start point or middle point).
	/// </summary>
	[DxfCodeValue(177)]
	public LinearParameterBaseLocation BaseLocation { get; set; }

	/// <summary>
	/// Gets or sets the first point.
	/// </summary>
	[DxfCodeValue(1010, 1020, 1030)]
	public XYZ FirstPoint { get; set; }

	/// <summary>
	/// Gets or sets the first point displacement in X direction.
	/// </summary>
	public EvalParameterProperty FirstPointDisplacementX { get; set; } = new EvalParameterProperty();

	/// <summary>
	/// Gets or sets the first point displacement in Y direction.
	/// </summary>
	public EvalParameterProperty FirstPointDisplacementY { get; set; } = new EvalParameterProperty();

	/// <summary>
	/// Gets or sets the grip ids.
	/// </summary>
	/// <remarks>
	/// Size 4 array of grip ids.
	/// </remarks>
	[DxfCollectionCodeValue(91)]
	[DxfCodeValue(DxfReferenceType.Count, 170)]
	public List<long> GripIds { get; } = new();

	/// <summary>
	/// Gets or sets the second point.
	/// </summary>
	[DxfCodeValue(1011, 1021, 1031)]
	public XYZ SecondPoint { get; set; }

	/// <summary>
	/// Gets or sets the second point displacement in X direction.
	/// </summary>
	public EvalParameterProperty SecondPointDisplacementX { get; set; } = new EvalParameterProperty();

	/// <summary>
	/// Gets or sets the second point displacement in Y direction.
	/// </summary>
	public EvalParameterProperty SecondPointDisplacementY { get; set; } = new EvalParameterProperty();

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.Block2PtParameter;

	/// <summary>
	/// Reads the first and second point's displacements (the "DisplacementX/Y" ports of the
	/// connected grips) from the context.
	/// </summary>
	protected void GetDisplacements(EvaluationContext context, out XYZ firstDisp, out XYZ secondDisp)
	{
		double fx = 0, fy = 0, sx = 0, sy = 0;
		ReadPort(this.FirstPointDisplacementX, "DisplacementX", context, out fx);
		ReadPort(this.FirstPointDisplacementY, "DisplacementY", context, out fy);
		ReadPort(this.SecondPointDisplacementX, "DisplacementX", context, out sx);
		ReadPort(this.SecondPointDisplacementY, "DisplacementY", context, out sy);

		firstDisp = new XYZ(fx, fy, 0);
		secondDisp = new XYZ(sx, sy, 0);
	}

	/// <summary>
	/// Writes the updated first/second point coordinates into the context under the
	/// "UpdatedBaseX/Y" and "UpdatedEndX/Y" ports.
	/// </summary>
	protected void WriteUpdatedPoints(EvaluationContext context, XYZ updatedFirst, XYZ updatedSecond)
	{
		context.SetValue(this.Id, "UpdatedBaseX", updatedFirst.X);
		context.SetValue(this.Id, "UpdatedBaseY", updatedFirst.Y);
		context.SetValue(this.Id, "UpdatedEndX", updatedSecond.X);
		context.SetValue(this.Id, "UpdatedEndY", updatedSecond.Y);
	}

	/// <summary>
	/// Writes the parameter's full set of point ports into the context.
	/// <para>
	/// The ports carry the base (first) and end (second) points, their displacements
	/// (zero when the connected grips are not activated), and the updated points
	/// (the stored points plus the displacements):
	/// <list type="bullet">
	/// <item><description>"BaseX/Y" / "EndX/Y" - the stored first/second point;</description></item>
	/// <item><description>"BaseXDelta/Y" / "EndXDelta/Y" - the first/second point displacement;</description></item>
	/// <item><description>"UpdatedBaseX/Y" / "UpdatedEndX/Y" - the updated first/second point;</description></item>
	/// <item><description>"Base" / "End" / "UpdatedBase" / "UpdatedEnd" - the same as point values.</description></item>
	/// </list>
	/// </para>
	/// </summary>
	protected void WritePorts(EvaluationContext context)
	{
		this.GetDisplacements(context, out XYZ firstDisp, out XYZ secondDisp);

		XYZ updatedFirst = this.FirstPoint + firstDisp;
		XYZ updatedSecond = this.SecondPoint + secondDisp;

		context.SetValue(this.Id, "BaseX", this.FirstPoint.X);
		context.SetValue(this.Id, "BaseY", this.FirstPoint.Y);
		context.SetValue(this.Id, "EndX", this.SecondPoint.X);
		context.SetValue(this.Id, "EndY", this.SecondPoint.Y);
		context.SetValue(this.Id, "BaseXDelta", firstDisp.X);
		context.SetValue(this.Id, "BaseYDelta", firstDisp.Y);
		context.SetValue(this.Id, "EndXDelta", secondDisp.X);
		context.SetValue(this.Id, "EndYDelta", secondDisp.Y);
		context.SetValue(this.Id, "UpdatedBaseX", updatedFirst.X);
		context.SetValue(this.Id, "UpdatedBaseY", updatedFirst.Y);
		context.SetValue(this.Id, "UpdatedEndX", updatedSecond.X);
		context.SetValue(this.Id, "UpdatedEndY", updatedSecond.Y);
		context.SetValue(this.Id, "Base", EvaluationValue.FromPoint(this.FirstPoint));
		context.SetValue(this.Id, "End", EvaluationValue.FromPoint(this.SecondPoint));
		context.SetValue(this.Id, "UpdatedBase", EvaluationValue.FromPoint(updatedFirst));
		context.SetValue(this.Id, "UpdatedEnd", EvaluationValue.FromPoint(updatedSecond));
	}

	/// <summary>
	/// Normalizes an angle to the [-pi, pi] range.
	/// </summary>
	protected static double NormalizeAngle(double angle)
	{
		while (angle > Math.PI) { angle -= 2.0 * Math.PI; }
		while (angle < -Math.PI) { angle += 2.0 * Math.PI; }
		return angle;
	}

	/// <summary>
	/// Computes the parameter's relative vector: the stored second point minus the stored
	/// first point, plus the (second minus first) point displacement.
	/// <para>
	/// This is the vector from the (updated) base point to the (updated) end point. In the
	/// initial state (no activated grips) it equals the stored <c>SecondPoint - FirstPoint</c>.
	/// </para>
	/// </summary>
	protected XYZ GetRelativeVector(EvaluationContext context)
	{
		this.GetDisplacements(context, out XYZ firstDisp, out XYZ secondDisp);
		return (this.SecondPoint - this.FirstPoint) + (secondDisp - firstDisp);
	}

	private static void ReadPort(EvalParameterProperty property, string port, EvaluationContext context, out double value)
	{
		value = 0;

		if (property != null && property.Connections.Count > 0)
		{
			int id = property.Connections[0].Id;
			context.TryGetValue(id, port, out value);
		}
	}
}