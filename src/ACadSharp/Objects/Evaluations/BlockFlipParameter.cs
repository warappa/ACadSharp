using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKFLIPPARAMETER object, used in AutoCAD to control a
/// flip in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockFlipParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockFlipParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockFlipParameter)]
[DxfSubClass(DxfSubclassMarker.BlockFlipParameter)]
public class BlockFlipParameter : Block2PtParameter, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the base state name.
	/// </summary>
	[DxfCodeValue(307)]
	public string BaseStateName { get; set; }

	/// <summary>
	/// Gets or sets the description.
	/// </summary>
	[DxfCodeValue(306)]
	public string Description { get; set; }

	/// <summary>
	/// Gets or sets the flipped state name.
	/// </summary>
	[DxfCodeValue(308)]
	public string FlippedStateName { get; set; }

	/// <summary>
	/// Gets or sets the label.
	/// </summary>
	[DxfCodeValue(305)]
	public string Label { get; set; }

	/// <summary>
	/// Gets or sets the label position.
	/// </summary>
	[DxfCodeValue(1012, 1022, 1032)]
	public XYZ LabelPosition { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockFlipParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockFlipParameter;

	/// <summary>
	/// Gets or sets the connection.
	/// </summary>
	public EvalConnection UpdatedFlipConnection { get; set; }

	/// <summary>
	/// Evaluates the flip parameter: reads the connected grips' displacements, updates the
	/// base and end points, and writes the flip state (0 = base state, 1 = flipped state)
	/// under the "UpdatedFlip" port.
	/// <para>
	/// The flip state is not derived from the geometry; it is toggled by the user. The
	/// default (unevaluated) state is 0 (the base state).
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		this.GetDisplacements(context, out XYZ firstDisp, out XYZ secondDisp);

		XYZ updatedFirst = this.FirstPoint + firstDisp;
		XYZ updatedSecond = this.SecondPoint + secondDisp;

		// The flip state: 0 = base state, 1 = flipped state. Default to 0 (not flipped).
		double flip = 0;

		context.SetValue(this.Id, "UpdatedFlip", flip);
		this.WriteUpdatedPoints(context, updatedFirst, updatedSecond);
		this.CurrentValue = flip;

		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockFlipParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockFlipParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}