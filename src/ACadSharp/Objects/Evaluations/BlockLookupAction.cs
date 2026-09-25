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
	/// context, finds the row where all input values match, and stores the matched output
	/// value as the action's current value.
	/// <para>
	/// Columns are matched by their data type: numeric columns (<see cref="ColumnData.IsText"/> = false)
	/// compare scalars within a tolerance; text columns (<see cref="ColumnData.IsText"/> = true)
	/// compare strings case-insensitively (a missing or non-string input never matches).
	/// The result is the matched cell of the first output (<see cref="ColumnData.IsLookupProperty"/>)
	/// column, shaped like that column (a string for a text column, a scalar for a numeric column);
	/// when no row matches, it is the column's <see cref="ColumnData.UnmatchedName"/> (a string for
	/// a text column, -1 for a numeric column); when the table has no output column, it is the
	/// matched row index (-1 when no row matches).
	/// The full AutoCAD semantics (writing the matched cells back to the connected parameters)
	/// are not implemented.
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		if (this.Columns.Count == 0)
		{
			base.CurrentValue = EvaluationValue.FromDouble(-1);
			return true;
		}

		// Read the input (non-output) columns' values: a scalar for numeric columns, a string for
		// text columns. The output columns (IsLookupProperty) are the lookup's own property columns
		// (written back to the connected parameters), so they are not read as inputs.
		int inputCount = 0;
		for (int col = 0; col < this.Columns.Count; col++)
		{
			if (!this.Columns[col].IsLookupProperty)
			{
				inputCount++;
			}
		}

		object[] inputValues = new object[inputCount];
		int inputIndex = 0;
		for (int col = 0; col < this.Columns.Count; col++)
		{
			ColumnData column = this.Columns[col];
			if (column.IsLookupProperty)
			{
				continue;
			}

			EvalConnection connection = new() { Id = column.NodeId, Name = column.ConnectionName };
			if (column.IsText)
			{
				string text = null;
				ReadConnectionValue(connection, context, out text);
				inputValues[inputIndex] = text; // null when the port is missing or not a string
			}
			else
			{
				double number = 0;
				ReadConnectionValue(connection, context, out number);
				inputValues[inputIndex] = number;
			}
			inputIndex++;
		}

		// Find the row where all input values match. With no input columns there is nothing to
		// match, so no row matches.
		int matchedRow = -1;
		if (inputCount > 0)
		{
			int rowCount = this.Columns[0].Rows.Count;
			for (int row = 0; row < rowCount; row++)
			{
				bool allMatch = true;
				int index = 0;
				for (int col = 0; col < this.Columns.Count; col++)
				{
					ColumnData column = this.Columns[col];
					if (column.IsLookupProperty)
					{
						continue;
					}

					if (!Matches(inputValues[index], column.Rows[row], column.IsText))
					{
						allMatch = false;
						break;
					}
					index++;
				}

				if (allMatch)
				{
					matchedRow = row;
					break;
				}
			}
		}

		// Write the matched cells back to the output columns' ports (the real AutoCAD behavior:
		// the lookup updates the connected parameters' text). A missing match writes the
		// UnmatchedName.
		for (int col = 0; col < this.Columns.Count; col++)
		{
			ColumnData column = this.Columns[col];
			if (!column.IsLookupProperty)
			{
				continue;
			}

			string cell = matchedRow >= 0 ? column.Rows[matchedRow] : column.UnmatchedName;
			EvalConnection connection = new() { Id = column.NodeId, Name = column.ConnectionName };
			if (column.IsText)
			{
				context.SetValue(connection.Id, connection.Name, cell);
			}
			else if (double.TryParse(cell, out double value))
			{
				context.SetValue(connection.Id, connection.Name, value);
			}
		}

		base.CurrentValue = this.GetResult(matchedRow);
		return true;
	}

	/// <summary>
	/// Computes the lookup result for a matched row index.
	/// <para>
	/// The result is the matched cell of the first output (<see cref="ColumnData.IsLookupProperty"/>)
	/// column, shaped like that column: a string for a text column, a scalar for a numeric column.
	/// When no row matches (<paramref name="matchedRow"/> &lt; 0), the result is the column's
	/// <see cref="ColumnData.UnmatchedName"/> (a string for a text column, -1 for a numeric column).
	/// When the table has no output column, the result is the matched row index as a scalar.
	/// </para>
	/// </summary>
	private EvaluationValue GetResult(int matchedRow)
	{
		ColumnData output = null;
		for (int col = 0; col < this.Columns.Count; col++)
		{
			if (this.Columns[col].IsLookupProperty)
			{
				output = this.Columns[col];
				break;
			}
		}

		if (output == null)
		{
			return EvaluationValue.FromDouble(matchedRow);
		}

		if (matchedRow < 0)
		{
			return output.IsText
				? EvaluationValue.FromString(output.UnmatchedName)
				: EvaluationValue.FromDouble(-1);
		}

		string cell = output.Rows[matchedRow];
		return output.IsText
			? EvaluationValue.FromString(cell)
			: EvaluationValue.FromDouble(double.TryParse(cell, out double value) ? value : 0);
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