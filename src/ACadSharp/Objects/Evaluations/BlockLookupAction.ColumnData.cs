using System.Collections.Generic;

namespace ACadSharp.Objects.Evaluations;

public partial class BlockLookupAction
{
	public class ColumnData
	{
		public string ConnectionName { get; set; }

		/// <summary>
		/// Whether this column holds text (string) values rather than numbers.
		/// <para>
		/// In a lookup table, a text column has <see cref="ValueType"/> = 1 (and <see cref="Type"/> = 0);
		/// a numeric column has <see cref="ValueType"/> = 40 (and <see cref="Type"/> = 2).
		/// Any other <see cref="ValueType"/> is treated as numeric (the documented limitation of
		/// the simplified evaluator).
		/// </para>
		/// </summary>
		public bool IsText => this.ValueType == 1;

		public bool IsLookupProperty { get; set; }

		public bool IsReadOnly { get; set; }

		public int NodeId { get; set; }

		public List<string> Rows { get; private set; } = new();

		public int Type { get; set; }

		public string UnmatchedName { get; set; }

		public int ValueType { get; set; }
	}
}