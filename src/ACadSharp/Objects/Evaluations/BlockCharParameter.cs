using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKCHARPARAMETER object — a dynamic-block parameter that holds a single
/// character.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockCharParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockCharParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockCharParameter)]
[DxfSubClass(DxfSubclassMarker.BlockCharParameter)]
public class BlockCharParameter : BlockParameter, IDxfClassDefined
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
	/// Gets or sets the character held by the parameter.
	/// </summary>
	[DxfCodeValue(305)]
	public char Value { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockCharParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockCharParameter;

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a character parameter this is the held character.
	/// </summary>
	public new EvaluationValue<char> CurrentValue => base.CurrentValue.As<char>();

	/// <summary>
	/// Evaluates the character parameter: writes the held character (the "Value" port) into the context.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		context.SetValue(this.Id, "Value", EvaluationValue.FromChar(this.Value));
		base.CurrentValue = EvaluationValue.FromChar(this.Value);

		return true;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockCharParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockCharParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
