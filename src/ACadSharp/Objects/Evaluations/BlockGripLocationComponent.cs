using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKGRIPLOCATIONCOMPONENT object.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockGripLocationComponent"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockGripExpression"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockGripLocationComponent)]
[DxfSubClass(DxfSubclassMarker.BlockGripExpression)]
public class BlockGripLocationComponent : EvaluationExpression, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the connection of the block grip location component.
	/// </summary>
	public EvalConnection Connection { get; set; } = new EvalConnection();

	/// <summary>
	/// Evaluates the component: reads the connected parameter's updated coordinate (the port
	/// named by <see cref="Connection"/>, for example "UpdatedEndX") from the context and
	/// stores it as the component's <see cref="EvaluationExpression.EvaluatedValue"/>.
	/// <para>
	/// A component mirrors a parameter's updated point coordinate: when the parameter's value
	/// changes, the component is updated with the new coordinate, and the grip it belongs to
	/// is rendered at that position.
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		if (this.Connection == null || this.Connection.Id == 0)
		{
			return true;
		}

		string port = this.Connection.Name;
		if (string.IsNullOrEmpty(port))
		{
			return true;
		}

		if (context.TryGetValue(this.Connection.Id, port, out double value))
		{
			this.EvaluatedValue = new DxfValuePair(DxfCode.Real, value);
			this.CurrentValue = value;
		}

		return true;
	}

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockGripLocationComponent;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockGripExpression;

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockGripExpression,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockGripLocationComponent,
			ItemClassId = 499,
			MaintenanceVersion = 20,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}