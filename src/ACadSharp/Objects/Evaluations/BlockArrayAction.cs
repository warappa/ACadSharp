using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKARRAYACTION object, used in AutoCAD to control a
/// array action in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockArrayAction"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockArrayAction"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockArrayAction)]
[DxfSubClass(DxfSubclassMarker.BlockArrayAction)]
public class BlockArrayAction : BlockAction, IDxfClassDefined
{
	public EvalConnection BaseConnection { get; set; } = new EvalConnection();

	[DxfCodeValue(141)]
	public double ColumnOffset { get; set; }

	public EvalConnection EndConnection { get; set; } = new EvalConnection();

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockArrayAction;

	[DxfCodeValue(140)]
	public double RowOffset { get; set; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockArrayAction;

	public EvalConnection UpdatedBaseConnection { get; set; } = new EvalConnection();

	public EvalConnection UpdatedEndConnection { get; set; } = new EvalConnection();

	/// <summary>
	/// Evaluates the array action: reads the base connection's value (the "Base" port of
	/// the connected parameter) from the context and stores it as the action's current
	/// value.
	/// </summary>
	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For an array action this is the driving value.
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	public override bool Evaluate(EvaluationContext context)
	{
		double value = 0;
		ReadConnectionValue(this.BaseConnection, context, out value);
		base.CurrentValue = EvaluationValue.FromDouble(value);
		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockArrayAction,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockArrayAction,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}