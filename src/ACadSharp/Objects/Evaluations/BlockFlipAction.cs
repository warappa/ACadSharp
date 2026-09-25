using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKFLIPACTION object, used in AutoCAD to control a
/// flip action in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockFlipAction"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockFlipAction"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockFlipAction)]
[DxfSubClass(DxfSubclassMarker.BlockFlipAction)]
public class BlockFlipAction : BlockAction, IDxfClassDefined
{
	public EvalConnection FlipConnection { get; set; } = new();

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockFlipAction;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockFlipAction;

	public EvalConnection UpdatedBaseConnection { get; set; } = new();

	public EvalConnection UpdatedEndConnection { get; set; } = new();

	public EvalConnection UpdatedFlipConnection { get; set; } = new();

	/// <summary>
	/// Evaluates the flip action: reads the flip state (the "Flip" port of the connected
	/// parameter) from the context and stores it as the action's current value.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		double flip = 0;
		ReadConnectionValue(this.FlipConnection, context, out flip);
		this.CurrentValue = flip;
		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockFlipAction,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockFlipAction,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}