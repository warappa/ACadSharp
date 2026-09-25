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
	/// This is a simplified evaluation: it matches rows by exact (within tolerance) equality
	/// of all input values. The full AutoCAD semantics (chained lookups, default values on
	/// no match) are not implemented.
	/// </para>
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		if (this.Columns.Count == 0)
		{
			this.CurrentValue = -1;
			return true;
		}

		// Read each column's input value.
		List<double> inputValues = new List<double>(this.Columns.Count);
		foreach (ColumnData column in this.Columns)
		{
			double value = 0;
			ReadConnectionValue(
				new EvalConnection { Id = column.NodeId, Name = column.ConnectionName },
				context,
				out value);
			inputValues.Add(value);
		}

		// Find the row where all input values match (within tolerance).
		const double tolerance = 1e-6;
		int matchedRow = -1;
		int rowCount = this.Columns[0].Rows.Count;
		for (int row = 0; row < rowCount; row++)
		{
			bool allMatch = true;
			for (int col = 0; col < this.Columns.Count; col++)
			{
				double cellValue = ParseCell(this.Columns[col].Rows[row]);
				if (Math.Abs(cellValue - inputValues[col]) > tolerance)
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

		this.CurrentValue = matchedRow;
		return true;
	}

	private static double ParseCell(string cell)
	{
		return double.TryParse(cell, out double value) ? value : 0;
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