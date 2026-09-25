using System;

namespace ACadSharp.Objects.Evaluations;

public partial class EvaluationGraph
{
	/// <summary>
	/// Flags for an <see cref="EvaluationGraph.Node"/>.
	/// <para>
	/// The meaning of the individual bits is not documented by AutoCAD. The only value
	/// observed in real files (all 10 bundled dynamic-block samples, every node) is
	/// <see cref="Bit5"/> (0x20). The bit names below are neutral placeholders; they are
	/// NOT the AutoCAD semantics.
	/// </para>
	/// </summary>
	[Flags]
	public enum NodeFlags
	{
		/// <summary>
		/// No flags are set.
		/// </summary>
		None = 0x00,

		/// <summary>
		/// Bit 0 (0x01) — meaning unknown.
		/// </summary>
		Bit0 = 0x01,

		/// <summary>
		/// Bit 1 (0x02) — meaning unknown.
		/// </summary>
		Bit1 = 0x02,

		/// <summary>
		/// Bit 2 (0x04) — meaning unknown.
		/// </summary>
		Bit2 = 0x04,

		/// <summary>
		/// Bit 3 (0x08) — meaning unknown.
		/// </summary>
		Bit3 = 0x08,

		/// <summary>
		/// Bit 4 (0x10) — meaning unknown.
		/// </summary>
		Bit4 = 0x10,

		/// <summary>
		/// Bit 5 (0x20) — the only value observed in real files; meaning unknown.
		/// </summary>
		Bit5 = 0x20,

		/// <summary>
		/// All bits are set.
		/// </summary>
		All = 0x2F
	};
}
