using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;
using System.Collections.Generic;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKSTRETCHACTION object, used in AutoCAD to control a
/// stretch action in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockStretchAction"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockStretchAction"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockStretchAction)]
[DxfSubClass(DxfSubclassMarker.BlockStretchAction)]
public class BlockStretchAction : StretchActionBase, IDxfClassDefined
{
	/// <inheritdoc/>
	[DxfCodeValue(141)]
	public override double AngleOffset { get; set; }

	/// <inheritdoc/>
	[DxfCollectionCodeValue(1011, 1021)]
	[DxfCodeValue(DxfReferenceType.Count, 72)]
	public override List<XY> Boundary { get; protected set; } = new();

	/// <inheritdoc/>
	[DxfCodeValue(140)]
	public override double DistanceMultiplier { get; set; }

	public EvalConnection EndXDeltaConnection { get; set; }

	public EvalConnection EndYDeltaConnection { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockStretchAction;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockStretchAction;

	/// <inheritdoc/>
	[DxfCodeValue(280)]
	public byte UnknownFlag { get; set; }

	/// <summary>
	/// Evaluates the stretch action: reads the X and Y deltas (the "EndXDelta/EndYDelta"
	/// ports of the connected parameter) from the context and stores the X delta as the
	/// action's current value.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		double x = 0, y = 0;
		ReadConnectionValue(this.EndXDeltaConnection, context, out x);
		ReadConnectionValue(this.EndYDeltaConnection, context, out y);
		this.CurrentValue = x;
		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockStretchAction,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockStretchAction,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}