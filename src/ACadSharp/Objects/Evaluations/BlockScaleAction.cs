using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

[DxfName(DxfFileToken.ObjectBlockScaleAction)]
[DxfSubClass(DxfSubclassMarker.BlockScaleAction)]
public class BlockScaleAction : BlockActionBasePt, IDxfClassDefined
{
	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockScaleAction;

	public EvalConnection ScaleConnection { get; set; } = new EvalConnection();

	public byte ScaleType { get; set; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockScaleAction;

	public EvalConnection XScaleConnection { get; set; } = new EvalConnection();

	public EvalConnection YScaleConnection { get; set; } = new EvalConnection();

	/// <summary>
	/// Evaluates the scale action: reads the scale factor (the "Scale" port of the connected
	/// parameter) from the context and stores it as the action's current value.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		double scale = 0;
		ReadConnectionValue(this.ScaleConnection, context, out scale);
		this.CurrentValue = scale;
		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockScaleAction,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockScaleAction,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}