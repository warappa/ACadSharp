using ACadSharp.Attributes;
using ACadSharp.Classes;
using System;
using System.Collections.Generic;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents a BLOCKLOOKUPACTION object, used in AutoCAD to control a
/// lookup action in a dynamic block.
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectBlockLookupAction"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockLookupAction"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectBlockLookupAction)]
[DxfSubClass(DxfSubclassMarker.BlockLookupAction)]
public partial class BlockLookupAction : BlockAction, IDxfClassDefined
{
	public List<ColumnData> Columns { get; set; } = new List<ColumnData>();

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectBlockLookupAction;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockLookupAction;

	[DxfCodeValue(280)]
	public bool UnknownFlag { get; set; }

	/// <summary>
	/// Evaluates the lookup action: reads each column's input value (the column's
	/// <see cref="ColumnData.NodeId"/> and <see cref="ColumnData.ConnectionName"/>) from the
	/// context, finds the row where all input values match, and stores the matched row
	/// index (or -1 when no row matches) as the action's current value.
	/// <para>
	/// Columns are matched by their data type: numeric columns (<see cref="ColumnData.IsText"/> = false)
	/// compare scalars within a tolerance; text columns (<see cref="ColumnData.IsText"/> = true)
	/// compare strings case-insensitively (a missing or non-string input never matches).
	/// The full AutoCAD semantics (chained lookups, default values on no match) are not implemented.
	/// </para>
	/// </summary>
	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a lookup action this is the matched row index (-1 when no row matches).
	/// </summary>
	public new EvaluationValue<double> CurrentValue => base.CurrentValue.As<double>();

	public override bool Evaluate(EvaluationContext context)
	{
		if (this.Columns.Count == 0)
		{
			base.CurrentValue = EvaluationValue.FromDouble(-1);
			return true;
		}

		// Read each column's input value: a scalar for numeric columns, a string for text columns.
		object[] inputValues = new object[this.Columns.Count];
		for (int col = 0; col < this.Columns.Count; col++)
		{
			ColumnData column = this.Columns[col];
			EvalConnection connection = new() { Id = column.NodeId, Name = column.ConnectionName };

			if (column.IsText)
			{
				string text = null;
				ReadConnectionValue(connection, context, out text);
				inputValues[col] = text; // null when the port is missing or not a string
			}
			else
			{
				double number = 0;
				ReadConnectionValue(connection, context, out number);
				inputValues[col] = number;
			}
		}

		// Find the row where all input values match.
		int matchedRow = -1;
		int rowCount = this.Columns[0].Rows.Count;
		for (int row = 0; row < rowCount; row++)
		{
			bool allMatch = true;
			for (int col = 0; col < this.Columns.Count; col++)
			{
				if (!Matches(inputValues[col], this.Columns[col].Rows[row], this.Columns[col].IsText))
				{
					allMatch = false;
					break;
				}
			}

			if (allMatch)
			{
				matchedRow = row;
				break;
			}
		}

		base.CurrentValue = EvaluationValue.FromDouble(matchedRow);
		return true;
	}

	/// <summary>
	/// Compares a column's input value against one of the column's cells.
	/// <para>
	/// A text column compares case-insensitively (a null input — missing or non-string value —
	/// never matches). A numeric column parses the cell as a scalar and compares within a
	/// tolerance (an unparseable cell never matches).
	/// </para>
	/// </summary>
	private static bool Matches(object input, string cell, bool isText)
	{
		const double tolerance = 1e-6;

		if (isText)
		{
			return input is string text && string.Equals(text, cell, StringComparison.OrdinalIgnoreCase);
		}

		return input is double value
			&& double.TryParse(cell, out double cellValue)
			&& Math.Abs(cellValue - value) <= tolerance;
	}

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockLookupAction,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectBlockLookupAction,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}