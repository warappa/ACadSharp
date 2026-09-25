using ACadSharp.Attributes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a dynamic block parameter that is defined by a single point in 3D space.
/// </summary>
[DxfSubClass(DxfSubclassMarker.Block1PtParameter)]
public abstract class Block1PtParameter : BlockParameter
{
	/// <summary>
	/// Gets or sets the displacement in the X direction for the parameter.
	/// </summary>
	public EvalParameterProperty DisplacementX { get; set; } = new();

	/// <summary>
	/// Gets or sets the displacement in the Y direction for the parameter.
	/// </summary>
	public EvalParameterProperty DisplacementY { get; set; } = new();

	/// <summary>
	/// Gets or sets the grip id.
	/// </summary>
	[DxfCodeValue(93)]
	public long GripId { get; set; }

	/// <summary>
	/// Location for parameter to be placed in the block.
	/// </summary>
	[DxfCodeValue(1010, 1020, 1030)]
	public XYZ Location { get; set; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.Block1PtParameter;

	/// <summary>
	/// Reads the parameter's displacement (the "DisplacementX/Y" ports of the connected
	/// grip) from the context.
	/// </summary>
	protected void GetDisplacement(EvaluationContext context, out XYZ displacement)
	{
		double x = 0, y = 0;
		ReadPort(this.DisplacementX, "DisplacementX", context, out x);
		ReadPort(this.DisplacementY, "DisplacementY", context, out y);

		displacement = new XYZ(x, y, 0);
	}

	/// <summary>
	/// Writes the updated location into the context under the "UpdatedX/Y" ports.
	/// </summary>
	protected void WriteUpdatedLocation(EvaluationContext context, XYZ updatedLocation)
	{
		context.SetValue(this.Id, "UpdatedX", updatedLocation.X);
		context.SetValue(this.Id, "UpdatedY", updatedLocation.Y);
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