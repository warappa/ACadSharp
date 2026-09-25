using ACadSharp.Attributes;

namespace ACadSharp.Objects.Evaluations;

public partial class EvaluationGraph
{
	public class Edge
	{
		/// <summary>
		/// Index of the previous edge in the <see cref="ToNodeIndex"/> node's incoming-edge
		/// list; <c>-1</c> when this edge is the first incoming edge of that node.
		/// </summary>
		[DxfCodeValue(92)]
		public int PrevInEdge { get; set; }

		/// <summary>
		/// Index of the next edge in the <see cref="ToNodeIndex"/> node's incoming-edge
		/// list; <c>-1</c> when this edge is the last incoming edge of that node.
		/// </summary>
		[DxfCodeValue(92)]
		public int NextInEdge { get; set; }

		/// <summary>
		/// Index of the previous edge in the <see cref="FromNodeIndex"/> node's
		/// outgoing-edge list; <c>-1</c> when this edge is the first outgoing edge of that node.
		/// </summary>
		[DxfCodeValue(92)]
		public int PrevOutEdge { get; set; }

		/// <summary>
		/// Index of the next edge in the <see cref="FromNodeIndex"/> node's outgoing-edge
		/// list; <c>-1</c> when this edge is the last outgoing edge of that node.
		/// </summary>
		[DxfCodeValue(92)]
		public int NextOutEdge { get; set; }

		/// <summary>
		/// Index of the paired reverse edge (only set for bidirectional lookup connections);
		/// <c>-1</c> when there is no reverse edge.
		/// </summary>
		[DxfCodeValue(92)]
		public int ReverseEdge { get; set; }

		/// <summary>
		/// Edge flags. <c>0</c> for normal edges; <c>4</c> for bidirectional lookup edges.
		/// </summary>
		[DxfCodeValue(93)]
		public int Flags { get; set; }

		/// <summary>
		/// Index of the source node (the element that produces the value).
		/// </summary>
		[DxfCodeValue(91)]
		public int FromNodeIndex { get; set; }

		/// <summary>
		/// Index of this edge in the edge list (0-based).
		/// </summary>
		[DxfCodeValue(92)]
		public int Index { get; set; }

		/// <summary>
		/// Index of the target node (the element that consumes the value).
		/// </summary>
		[DxfCodeValue(91)]
		public int ToNodeIndex { get; set; }

		/// <summary>
		/// The number of "wires" (parallel connection channels) this edge carries: the count
		/// of port-connections on the target element bound to the source element, with a
		/// grip-binding/positional fallback of 1, and for lookup edges the number of table
		/// columns the source element feeds.
		/// </summary>
		[DxfCodeValue(94)]
		public int TrackedCount { get; set; }

		public Edge Clone()
		{
			return (Edge)MemberwiseClone();
		}
	}
}