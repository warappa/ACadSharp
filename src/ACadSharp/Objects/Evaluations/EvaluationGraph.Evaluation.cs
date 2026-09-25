using System.Collections.Generic;

namespace ACadSharp.Objects.Evaluations;

public partial class EvaluationGraph
{
	private readonly HashSet<int> _activatedNodes = new HashSet<int>();

	private EvaluationContext _context = new EvaluationContext();

	/// <summary>
	/// Gets the evaluation context (the value store used to pass values between nodes).
	/// </summary>
	public EvaluationContext Context => this._context;

	/// <summary>
	/// Marks the given nodes as activated (user-touched).
	/// <para>
	/// Mirrors the ObjectARX <c>AcDbEvalGraph::activate(nodes)</c>. The activated nodes are
	/// the starting points of the evaluation: the subgraph reachable from them (following
	/// outgoing edges) is the subgraph that gets evaluated.
	/// </para>
	/// </summary>
	/// <param name="nodeIndices">The indices of the nodes to activate.</param>
	public void Activate(IEnumerable<int> nodeIndices)
	{
		this._activatedNodes.Clear();

		foreach (int i in nodeIndices)
		{
			if (i >= 0 && i < this._nodes.Count)
			{
				this._activatedNodes.Add(i);
			}
		}
	}

	/// <summary>
	/// Checks whether a node is activated.
	/// </summary>
	public bool IsActivated(int nodeIndex)
	{
		return this._activatedNodes.Contains(nodeIndex);
	}

	/// <summary>
	/// Evaluates the graph: topologically sorts the subgraph reachable from the activated
	/// nodes and traverses it in order, calling <see cref="EvaluationExpression.Evaluate"/>
	/// on each node.
	/// <para>
	/// Mirrors the ObjectARX <c>AcDbEvalGraph::evaluate()</c>: the reachable subgraph is
	/// traversed in topological order, and each node's <c>evaluate()</c> is invoked. A node's
	/// failure aborts the evaluation.
	/// </para>
	/// </summary>
	/// <returns>True when the evaluation succeeded; false when a cycle was detected or a
	/// node's evaluation failed.</returns>
	public bool Evaluate()
	{
		this._context.Clear();

		List<int> order = this.GetTopologicalOrder(this._activatedNodes);
		if (order.Count == 0)
		{
			return false; // cycle detected (or no activated nodes)
		}

		foreach (int i in order)
		{
			Node node = this._nodes[i];
			if (node.Expression == null)
			{
				continue;
			}

			if (!node.Expression.Evaluate(this._context))
			{
				return false; // a node's evaluation failed; abort
			}
		}

		return true;
	}
}
