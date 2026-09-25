using ACadSharp.Attributes;

namespace ACadSharp.Objects.Evaluations;

public partial class EvaluationGraph
{
	/// <summary>
	/// Represents a graph node of a <see cref="EvaluationGraph"/>.
	/// </summary>
	public class Node
	{
		/// <summary>
		/// Index of the first edge in this node's incoming-edge list (an edge whose
		/// <see cref="Edge.ToNodeIndex"/> is this node); <c>-1</c> when the node has no
		/// incoming edges.
		/// </summary>
		[DxfCodeValue(92)]
		public int FirstInEdge { get; internal set; }

		/// <summary>
		/// Index of the last edge in this node's incoming-edge list; <c>-1</c> when the
		/// node has no incoming edges.
		/// </summary>
		[DxfCodeValue(92)]
		public int LastInEdge { get; internal set; }

		/// <summary>
		/// Index of the first edge in this node's outgoing-edge list (an edge whose
		/// <see cref="Edge.FromNodeIndex"/> is this node); <c>-1</c> when the node has no
		/// outgoing edges.
		/// </summary>
		[DxfCodeValue(92)]
		public int FirstOutEdge { get; internal set; }

		/// <summary>
		/// Index of the last edge in this node's outgoing-edge list; <c>-1</c> when the
		/// node has no outgoing edges.
		/// </summary>
		[DxfCodeValue(92)]
		public int LastOutEdge { get; internal set; }

		/// <summary>
		/// Gets a <see cref="EvaluationExpression"/> associated with this <see cref="Node"/>.
		/// </summary>
		[DxfCodeValue(360)]
		public EvaluationExpression Expression
		{
			get
			{
				return this._expression;
			}
			set
			{
				if (value != null)
				{
					this._evaluationGraph.Document?.AddCadObject(value);
				}

				if (this._expression != null)
				{
					this._evaluationGraph.Document?.RemoveCadObject(this._expression);
				}

				this._expression = value;
				this._expression.Owner = this._evaluationGraph;
			}
		}

		/// <summary>
		/// Gets or sets the flags of this <see cref="Node"/>.
		/// </summary>
		[DxfCodeValue(93)]
		public NodeFlags Flags { get; set; }

		/// <summary>
		/// Gets or sets the index of the next <see cref="Node"/> in the list of
		/// graph nodes in the owning <see cref="EvaluationGraph"/>.
		/// </summary>
		[DxfCodeValue(95)]
		public int Id { get; set; }

		/// <summary>
		/// Gets or sets the index of this <see cref="Node"/> in the list of
		/// graph nodes in the owning <see cref="EvaluationGraph"/>.
		/// </summary>
		[DxfCodeValue(91)]
		public int Index { get; set; }

		private EvaluationExpression _expression;

		private EvaluationGraph _evaluationGraph;

		public Node(EvaluationGraph evaluationGraph)
		{
			this._evaluationGraph = evaluationGraph;
		}

		/// <summary>
		/// Creates a deep copy of this <see cref="Node"/>.
		/// </summary>
		/// <returns>A deep copy of this <see cref="Node"/>.</returns>
		public Node Clone()
		{
			Node clone = (Node)this.MemberwiseClone();

			clone.Expression = (EvaluationExpression)this.Expression?.Clone();

			return clone;
		}
	}
}