using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKFLIPGRIP object.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockFlipGrip"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockFlipGrip"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockFlipGrip)]
[DxfSubClass(DxfSubclassMarker.BlockFlipGrip)]
public class BlockFlipGrip : BlockGrip, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the X component of the direction vector.
	/// </summary>
	[DxfCodeValue(140)]
	public double DirectionX { get; set; }

	/// <summary>
	/// Gets or sets the Y component of the direction vector.
	/// </summary>
	[DxfCodeValue(141)]
	public double DirectionY { get; set; }

	/// <summary>
	/// Gets or sets the Z component of the direction vector.
	/// </summary>
	[DxfCodeValue(142)]
	public double DirectionZ { get; set; }

	/// <summary>
	/// Gets or sets the expression id.
	/// </summary>
	[DxfCodeValue(93)]
	public int FlipExpressionId { get; set; }

	/// <summary>
	/// Evaluates the flip grip: writes the base grip ports (displacement and updated
	/// location) and the "UpdatedFlip" port (the flip state, 0 = not flipped).
	/// <para>
	/// The flip state is a discrete toggle; without a user interaction the grip is in its
	/// base (not flipped) state.
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		bool result = base.Evaluate(context);
		context.SetValue(this.Id, "UpdatedFlip", 0.0);
		return result;
	}

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockFlipGrip;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockFlipGrip;

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockFlipGrip,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockFlipGrip,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}