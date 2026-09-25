using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKHANDLEPARAMETER object — a dynamic-block parameter that holds an
/// object reference (a handle).
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockHandleParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockHandleParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockHandleParameter)]
[DxfSubClass(DxfSubclassMarker.BlockHandleParameter)]
public class BlockHandleParameter : BlockParameter, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the description.
	/// </summary>
	[DxfCodeValue(304)]
	public string Description { get; set; }

	/// <summary>
	/// Gets or sets the label text.
	/// </summary>
	[DxfCodeValue(303)]
	public string Label { get; set; }

	/// <summary>
	/// Gets or sets the object handle held by the parameter.
	/// </summary>
	[DxfCodeValue(94)]
	public long Value { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockHandleParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockHandleParameter;

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a handle parameter this is the held object handle.
	/// </summary>
	public new EvaluationValue<long> CurrentValue => base.CurrentValue.As<long>();

	/// <summary>
	/// Evaluates the handle parameter: writes the held object handle (the "Value" port) into the context.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		context.SetValue(this.Id, "Value", EvaluationValue.FromObjectId(this.Value));
		base.CurrentValue = EvaluationValue.FromObjectId(this.Value);

		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockHandleParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockHandleParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
