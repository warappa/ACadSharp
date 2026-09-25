using System;

namespace ACadSharp.Objects.Evaluations;

public partial class EvaluationGraph
{
	/// <summary>
	/// Flags for an <see cref="EvaluationGraph.Edge"/>.
	/// <para>
	/// <see cref="Invertible"/> (0x04) marks a bidirectional lookup connection: the file
	/// stores two edge records (one per direction) that together represent a single
	/// invertible edge. During activation AutoCAD activates one direction and suppresses
	/// the reverse. The remaining bits are not documented; their names are neutral
	/// placeholders, NOT the AutoCAD semantics.
	/// </para>
	/// </summary>
	[Flags]
	public enum EdgeFlags
	{
		/// <summary>
		/// No flags are set. A normal directed edge.
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
		/// Bit 2 (0x04) — the edge is part of a bidirectional (invertible) lookup
		/// connection. The file stores two edge records (one per direction) linked
		/// via <see cref="Edge.ReverseEdge"/>; together they represent a single
		/// invertible edge that may be activated from either endpoint.
		/// </summary>
		Invertible = 0x04,

		/// <summary>
		/// Bit 3 (0x08) — meaning unknown.
		/// </summary>
		Bit3 = 0x08,

		/// <summary>
		/// Bit 4 (0x10) — meaning unknown.
		/// </summary>
		Bit4 = 0x10,

		/// <summary>
		/// Bit 5 (0x20) — meaning unknown.
		/// </summary>
		Bit5 = 0x20,

		/// <summary>
		/// All bits are set.
		/// </summary>
		All = 0x3F
	};
}
