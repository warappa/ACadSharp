using System.Collections.Generic;
using ACadSharp.Attributes;
using ACadSharp.Entities;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

[DxfSubClass(DxfSubclassMarker.BlockAction)]
public abstract class BlockAction : BlockElement
{
	/// <summary>
	/// Gets the list of <see cref="Entity"/> objects affected by this <see cref="BlockAction"/>.
	/// </summary>
	[DxfCodeValue(DxfReferenceType.Count, 71)]
	[DxfCollectionCodeValue(DxfReferenceType.Handle, 330)]
	public List<Entity> Entities { get; } = new List<Entity>();

	/// <summary>
	/// Gets or sets the position of the action label.
	/// </summary>
	[DxfCodeValue(1010, 1020, 1030)]
	public XYZ LabelPosition { get; set; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockAction;

	[DxfCodeValue(DxfReferenceType.Count, 70)]
	[DxfCollectionCodeValue(91)]
	public List<int> ParametersIds { get; } = new();

	/// <summary>
	/// Reads a value from the context via an <see cref="EvalConnection"/> (the connection's
	/// target expression id and port name). Returns false (and <paramref name="value"/> = 0)
	/// when the connection is empty or the value is not present.
	/// </summary>
	protected static bool ReadConnectionValue(EvalConnection connection, EvaluationContext context, out double value)
	{
		value = 0;

		if (connection == null || connection.Id == 0 || string.IsNullOrEmpty(connection.Name))
		{
			return false;
		}

		return context.TryGetValue(connection.Id, connection.Name, out value);
	}
}