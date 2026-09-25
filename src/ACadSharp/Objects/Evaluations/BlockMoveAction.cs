using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKMOVEACTION object, used in AutoCAD to control a
/// movement action in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockMoveAction"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockMoveAction"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockMoveAction)]
[DxfSubClass(DxfSubclassMarker.BlockMoveAction)]
public class BlockMoveAction : BlockAction, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the angle offset for the move action.
	/// </summary>
	[DxfCodeValue(141)]
	public double AngleOffset { get; set; }

	/// <summary>
	/// Gets or sets the distance multiplier for the move action.
	/// </summary>
	[DxfCodeValue(140)]
	public double DistanceMultiplier { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockMoveAction;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockMoveAction;

	/// <summary>
	/// Gets or sets an unknown flag value.
	/// </summary>
	[DxfCodeValue(280)]
	public byte UnknownFlag { get; set; }

	/// <summary>
	/// Gets or sets the evaluation connection for the X delta displacement.
	/// </summary>
	public EvalConnection XDeltaConnection { get; set; }

	/// <summary>
	/// Gets or sets the evaluation connection for the Y delta displacement.
	/// </summary>
	public EvalConnection YDeltaConnection { get; set; }

	/// <summary>
	/// Evaluates the move action: reads the X and Y deltas (the "XDelta/YDelta" ports of the
	/// connected parameter) from the context and stores the X delta as the action's current
	/// value.
	/// </summary>
	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a move action this is the full (X, Y) displacement.
	/// </summary>
	public new EvaluationValue<XYZ> CurrentValue => base.CurrentValue.As<XYZ>();

	public override bool Evaluate(EvaluationContext context)
	{
		double x = 0, y = 0;
		ReadConnectionValue(this.XDeltaConnection, context, out x);
		ReadConnectionValue(this.YDeltaConnection, context, out y);
		base.CurrentValue = EvaluationValue.FromPoint(new XYZ(x, y, 0));
		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockMoveAction,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockMoveAction,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}