using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKTEXTPARAMETER object — a dynamic-block parameter that holds a fixed
/// text value.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockTextParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockTextParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockTextParameter)]
[DxfSubClass(DxfSubclassMarker.BlockTextParameter)]
public class BlockTextParameter : BlockParameter, IDxfClassDefined
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
	/// Gets or sets the text value held by the parameter.
	/// </summary>
	[DxfCodeValue(305)]
	public string Value { get; set; } = string.Empty;

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockTextParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockTextParameter;

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a text parameter this is the held text.
	/// </summary>
	public new EvaluationValue<string> CurrentValue => base.CurrentValue.As<string>();

	/// <summary>
	/// Evaluates the text parameter: writes the held text (the "Value" port) into the context.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		context.SetValue(this.Id, "Value", EvaluationValue.FromString(this.Value));
		base.CurrentValue = EvaluationValue.FromString(this.Value);

		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockTextParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockTextParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
