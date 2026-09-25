using System;
using System.Collections.Generic;
using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;
using ACadSharp.Entities;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKVISIBILITYPARAMETER object, in AutoCAD used to
/// control the visibility state of entities in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockVisibilityParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockVisibilityParameter"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockVisibilityParameter)]
[DxfSubClass(DxfSubclassMarker.BlockVisibilityParameter)]
public partial class BlockVisibilityParameter : Block1PtParameter, IDxfClassDefined
{
	/// <summary>
	/// Visibility parameter description.
	/// </summary>
	[DxfCodeValue(302)]
	public string Description { get; set; }

	/// <summary>
	/// Gets the list of all <see cref="Entity"/> objects of the dynamic block
	/// this <see cref="BlockVisibilityParameter"/> is associated with.
	/// </summary>
	[DxfCollectionCodeValue(331)]
	[DxfCodeValue(DxfReferenceType.Count, 93)]
	public List<Entity> Entities { get; private set; } = new List<Entity>();

	/// <summary>
	/// Visibility parameter label.
	/// </summary>
	[DxfCodeValue(301)]
	public string Label { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockVisibilityParameter;

	/// <summary>
	/// Gets the list of states each containing a 2 subsets of <see cref="Entity"/> <br/>
	/// Objects must belong to the dynamic <see cref="BlockVisibilityParameter"/> associated with.
	/// </summary>
	[DxfCodeValue(DxfReferenceType.Count, 92)]
	public IReadOnlyDictionary<string, State> States { get => _states; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockVisibilityParameter;

	[DxfCodeValue(281)]
	public bool Value281 { get; set; }

	[DxfCodeValue(91)]
	public bool Value91 { get; set; }

	private Dictionary<string, State> _states = new(StringComparer.InvariantCultureIgnoreCase);

	/// <summary>
	/// Adds a state to the collection using the state's name as the key.
	/// </summary>
	/// <param name="state">The state to add to the collection. Cannot be null. The state's name must be unique within the collection.</param>
	public void AddState(State state)
	{
		this._states.Add(state.Name, state);
	}

	/// <summary>
	/// Evaluates the visibility parameter: reads the connected grip's displacement, updates
	/// the location, and writes the selected state index under the "Value" port.
	/// <para>
	/// The selected state is not derived from the geometry; it is chosen by the user. The
	/// default (unevaluated) state is index 0 (the first state).
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		this.GetDisplacement(context, out XYZ displacement);

		XYZ updatedLocation = this.Location + displacement;

		// The selected state index: 0 = the first state (default).
		double stateIndex = 0;

		context.SetValue(this.Id, "Value", stateIndex);
		this.WriteUpdatedLocation(context, updatedLocation);
		base.CurrentValue = EvaluationValue.FromDouble(stateIndex);

		return true;
	}

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a visibility parameter this is the selected state index.
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	/// <inheritdoc/>
	public override CadObject Clone()
	{
		BlockVisibilityParameter clone = (BlockVisibilityParameter)base.Clone();

		clone.Entities = new List<Entity>();
		foreach (var item in this.Entities)
		{
			clone.Entities.Add((Entity)item.Clone());
		}

		clone._states = new(StringComparer.InvariantCultureIgnoreCase);
		foreach (State item in this._states.Values)
		{
			var state = (State)item.Clone();
			clone._states.Add(state.Name, state);
		}

		return clone;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockVisibilityParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockVisibilityParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}