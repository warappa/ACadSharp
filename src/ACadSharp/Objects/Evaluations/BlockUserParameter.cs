using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKUSERPARAMETER object, used in AutoCAD to hold a user-settable value in
/// a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockUserParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockUserParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockUserParameter)]
[DxfSubClass(DxfSubclassMarker.BlockUserParameter)]
public class BlockUserParameter : BlockParameter, IDxfClassDefined
{
	/// <summary>
	/// The current value of the parameter (a user-settable numeric value).
	/// </summary>
	[DxfCodeValue(140)]
	public double Value { get; set; }

	/// <summary>
	/// The value set (the allowed values).
	/// </summary>
	public ParameterValueSet ValueSet { get; set; } = new ParameterValueSet();

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockUserParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockUserParameter;

	/// <summary>
	/// Evaluates the user parameter: emits the current user value as the "Value" port.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		context.SetValue(this.Id, "Value", this.Value);
		base.CurrentValue = EvaluationValue.FromDouble(this.Value);
		return true;
	}

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a user parameter this is the user value.
	/// <para>
	/// Falls back to the stored <see cref="Value"/> (the user's last input) when the node has not
	/// been evaluated yet (the evaluation result is unset), so the parameter always exposes its
	/// current value rather than an empty placeholder.
	/// </para>
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	/// <inheritdoc/>
	protected override EvaluationValue GetDefaultValue() => EvaluationValue.FromDouble(this.Value);

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockUserParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockUserParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
