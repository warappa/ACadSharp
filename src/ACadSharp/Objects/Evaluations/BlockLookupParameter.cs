using ACadSharp.Attributes;
using ACadSharp.Classes;
using CSMath;
using System;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKLOOKUPPARAMETER object, used in AutoCAD to drive block geometry from
/// a lookup table in the Block Properties Table (the "Lookup" rows of the Customize tab of
/// a dynamic block).
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockLookupParameter"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockLookupParameter"/> <br/>
/// <br/>
/// A lookup parameter is the <em>lookup property</em> (output) column of a
/// <see cref="BlockLookupAction"/> table. Its value is therefore <b>table-driven and
/// type-variable</b>: it is whatever the bound table column produces — a <b>string</b> for a
/// text column (DXF <c>95</c> = 1) and a <b>scalar</b> for a numeric column (<c>95</c> = 40).
/// It is <b>not</b> inherently an (X, Y, Z) point, so this class deliberately does not expose
/// a typed <see cref="EvaluationValue{T}"/> view; the type-agnostic
/// <see cref="EvaluationExpression.CurrentValue"/> is the correct access.
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockLookupParameter)]
[DxfSubClass(DxfSubclassMarker.BlockLookupParameter)]
public class BlockLookupParameter : Block1PtParameter, IDxfClassDefined
{
	/// <summary>
	/// Gets or sets the action ID associated with the block lookup parameter. This ID (code 94)
	/// is the creation number of the <see cref="BlockLookupAction"/> the parameter is bound to —
	/// the lookup table whose column defines this parameter's value and its type.
	/// </summary>
	[DxfCodeValue(94)]
	public int ActionId { get; set; }

	/// <summary>
	/// Gets or sets the description of the block lookup parameter. This description is used to provide additional information about the parameter in the Block Properties Table and in the Customize tab of a dynamic block.
	/// </summary>
	[DxfCodeValue(304)]
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the label of the block lookup parameter. This label is used to identify the parameter in the Block Properties Table and in the Customize tab of a dynamic block.
	/// </summary>
	[DxfCodeValue(303)]
	public string Label { get; set; } = string.Empty;

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockLookupParameter;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockLookupParameter;

	/// <summary>
	/// Evaluates the lookup parameter.
	/// <para>
	/// The value of a lookup parameter is table-driven and type-variable (a string for a text
	/// column, a scalar for a numeric column), so it is computed from the bound
	/// <see cref="BlockLookupAction"/> column rather than from the grip displacement:
	/// <list type="bullet">
	/// <item>the matched cell the lookup action wrote to this parameter's port
	/// (<c>(this.Id, column.ConnectionName)</c>), <b>with its shape preserved</b> — a string stays
	/// a string, a scalar stays a scalar; and</item>
	/// <item>when the action did not write a value (the table was not applied in this pass), the
	/// column's <see cref="BlockLookupAction.ColumnData.UnmatchedName"/> default, shaped by the
	/// column's type.</item>
	/// </list>
	/// The (1-point) location still propagates to the <c>UpdatedX/Y</c> ports for geometry, but the
	/// location is <em>not</em> the value.
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		// The location (this is a 1-point parameter) still propagates for geometry.
		this.GetDisplacement(context, out XYZ displacement);
		this.WriteUpdatedLocation(context, this.Location + displacement);

		// The value is the table-driven value of this parameter's column.
		base.CurrentValue = this.GetTableValue(context);

		return true;
	}

	/// <inheritdoc/>
	protected override EvaluationValue GetDefaultValue()
	{
		// The value is table-driven. The natural default (a row that never matched) is the bound
		// column's UnmatchedName, shaped by the column's type (a string for a text column, a
		// scalar for a numeric column). When the bound column cannot be resolved (for example a
		// standalone parameter with no owning graph), there is no value.
		if (this.TryGetBoundColumn(out BlockLookupAction.ColumnData column))
		{
			return column.IsText
				? EvaluationValue.FromString(column.UnmatchedName ?? string.Empty)
				: EvaluationValue.FromDouble(ParseCell(column.UnmatchedName));
		}

		return EvaluationValue.None;
	}

	/// <summary>
	/// The table-driven value of this parameter's column: the matched cell the lookup action
	/// wrote to this parameter's port (shape preserved), or — when the action did not write a
	/// value — the column's default (<see cref="BlockLookupAction.ColumnData.UnmatchedName"/>,
	/// shaped by the column's type).
	/// </summary>
	private EvaluationValue GetTableValue(EvaluationContext context)
	{
		if (!this.TryGetBoundColumn(out BlockLookupAction.ColumnData column))
		{
			return EvaluationValue.None;
		}

		// The action writes the matched cell to (column.NodeId, column.ConnectionName); this
		// parameter is that node, so read its own port (shape preserved).
		if (context != null
			&& context.TryGetValue(this.Id, column.ConnectionName, out EvaluationValue value)
			&& value.Type != EvaluationValueType.None)
		{
			return value;
		}

		// The table was not applied in this pass: fall back to the column's default.
		return this.GetDefaultValue();
	}

	/// <summary>
	/// Resolves the <see cref="BlockLookupAction"/> this parameter is bound to (via
	/// <see cref="ActionId"/>) and the column of that table this parameter is the value of (the
	/// column whose input element is this parameter, i.e. <c>NodeId == this.Id</c>).
	/// </summary>
	/// <param name="column">The bound table column.</param>
	/// <returns>True when the bound column was resolved; otherwise false.</returns>
	private bool TryGetBoundColumn(out BlockLookupAction.ColumnData column)
	{
		column = null;

		EvaluationGraph graph = this.Owner as EvaluationGraph;
		if (graph == null || this.ActionId == 0)
		{
			return false;
		}

		BlockLookupAction action = graph.GetNodeExpression(this.ActionId) as BlockLookupAction;
		if (action == null)
		{
			return false;
		}

		for (int i = 0; i < action.Columns.Count; i++)
		{
			if (action.Columns[i].NodeId == this.Id)
			{
				column = action.Columns[i];
				return true;
			}
		}

		return false;
	}

	/// <summary>Parses a table cell as a scalar; 0 when the cell is not a number.</summary>
	private static double ParseCell(string cell) => double.TryParse(cell, out double value) ? value : 0;

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockLookupParameter,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockLookupParameter,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
