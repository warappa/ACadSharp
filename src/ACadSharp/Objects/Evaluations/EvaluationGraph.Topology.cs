using System;
using System.Collections.Generic;
using System.Text;

namespace ACadSharp.Objects.Evaluations;

public partial class EvaluationGraph
{
	/// <summary>
	/// Returns the indices of the edges that go INTO the given node (the node's incoming
	/// edges), in linked-list order. Returns an empty list for an invalid node index or a
	/// node with no incoming edges.
	/// </summary>
	/// <param name="nodeIndex">The index of the node.</param>
	public List<int> GetIncomingEdges(int nodeIndex)
	{
		List<int> result = new List<int>();

		if (nodeIndex < 0 || nodeIndex >= this._nodes.Count)
		{
			return result;
		}

		int current = this._nodes[nodeIndex].FirstInEdge;
		while (current != -1)
		{
			if (current < 0 || current >= this.Edges.Count)
			{
				break; // corrupt data; stop walking
			}

			result.Add(current);
			current = this.Edges[current].NextInEdge;
		}

		return result;
	}

	/// <summary>
	/// Returns the indices of the edges that go OUT of the given node (the node's outgoing
	/// edges), in linked-list order. Returns an empty list for an invalid node index or a
	/// node with no outgoing edges.
	/// </summary>
	/// <param name="nodeIndex">The index of the node.</param>
	public List<int> GetOutgoingEdges(int nodeIndex)
	{
		List<int> result = new List<int>();

		if (nodeIndex < 0 || nodeIndex >= this._nodes.Count)
		{
			return result;
		}

		int current = this._nodes[nodeIndex].FirstOutEdge;
		while (current != -1)
		{
			if (current < 0 || current >= this.Edges.Count)
			{
				break; // corrupt data; stop walking
			}

			result.Add(current);
			current = this.Edges[current].NextOutEdge;
		}

		return result;
	}

	/// <summary>
	/// Validates the internal consistency of the graph's node/edge linked-list data:
	/// each node's first/last in/out edge markers match the actual linked lists, and each
	/// edge's prev/next pointers are consistent with the node lists.
	/// </summary>
	/// <param name="error">A description of the first inconsistency found, or empty when valid.</param>
	/// <returns>True when the graph data is internally consistent.</returns>
	public bool TryValidate(out string error)
	{
		error = string.Empty;

		if (this._nodes.Count == 0)
		{
			return true;
		}

		// Build the expected in/out edge lists by scanning all edges (ground truth).
		List<List<int>> expectedIn = new List<List<int>>(this._nodes.Count);
		List<List<int>> expectedOut = new List<List<int>>(this._nodes.Count);
		for (int i = 0; i < this._nodes.Count; i++)
		{
			expectedIn.Add(new List<int>());
			expectedOut.Add(new List<int>());
		}

		for (int i = 0; i < this.Edges.Count; i++)
		{
			Edge e = this.Edges[i];
			if (e.FromNodeIndex < 0 || e.FromNodeIndex >= this._nodes.Count || e.ToNodeIndex < 0 || e.ToNodeIndex >= this._nodes.Count)
			{
				error = $"edge {i} references an out-of-range node (from={e.FromNodeIndex}, to={e.ToNodeIndex})";
				return false;
			}

			expectedOut[e.FromNodeIndex].Add(i);
			expectedIn[e.ToNodeIndex].Add(i);
		}

		// Check each node's in/out edge markers against the expected lists.
		for (int i = 0; i < this._nodes.Count; i++)
		{
			Node n = this._nodes[i];

			List<int> inList = this.GetIncomingEdges(i);
			List<int> outList = this.GetOutgoingEdges(i);

			// The walked list must equal the expected list (order included).
			if (!ListsEqual(inList, expectedIn[i]))
			{
				error = $"node {i} incoming-edge list mismatch: walked=[{string.Join(",", inList)}] expected=[{string.Join(",", expectedIn[i])}]";
				return false;
			}

			if (!ListsEqual(outList, expectedOut[i]))
			{
				error = $"node {i} outgoing-edge list mismatch: walked=[{string.Join(",", outList)}] expected=[{string.Join(",", expectedOut[i])}]";
				return false;
			}

			// The first/last markers must match the list ends.
			int expFirstIn = expectedIn[i].Count == 0 ? -1 : expectedIn[i][0];
			int expLastIn = expectedIn[i].Count == 0 ? -1 : expectedIn[i][expectedIn[i].Count - 1];
			int expFirstOut = expectedOut[i].Count == 0 ? -1 : expectedOut[i][0];
			int expLastOut = expectedOut[i].Count == 0 ? -1 : expectedOut[i][expectedOut[i].Count - 1];

			if (n.FirstInEdge != expFirstIn || n.LastInEdge != expLastIn || n.FirstOutEdge != expFirstOut || n.LastOutEdge != expLastOut)
			{
				error = $"node {i} edge markers mismatch: firstIn={n.FirstInEdge} lastIn={n.LastInEdge} firstOut={n.FirstOutEdge} lastOut={n.LastOutEdge} (expected {expFirstIn}/{expLastIn}/{expFirstOut}/{expLastOut})";
				return false;
			}
		}

		// Check each edge's prev/next pointers against the node lists.
		for (int i = 0; i < this.Edges.Count; i++)
		{
			Edge e = this.Edges[i];

			List<int> inList = expectedIn[e.ToNodeIndex];
			List<int> outList = expectedOut[e.FromNodeIndex];

			int pi = inList.IndexOf(i);
			int po = outList.IndexOf(i);

			int expPrevIn = pi > 0 ? inList[pi - 1] : -1;
			int expNextIn = pi + 1 < inList.Count ? inList[pi + 1] : -1;
			int expPrevOut = po > 0 ? outList[po - 1] : -1;
			int expNextOut = po + 1 < outList.Count ? outList[po + 1] : -1;

			if (e.PrevInEdge != expPrevIn || e.NextInEdge != expNextIn || e.PrevOutEdge != expPrevOut || e.NextOutEdge != expNextOut)
			{
				error = $"edge {i} prev/next pointers mismatch: prevIn={e.PrevInEdge} nextIn={e.NextInEdge} prevOut={e.PrevOutEdge} nextOut={e.NextOutEdge} (expected {expPrevIn}/{expNextIn}/{expPrevOut}/{expNextOut})";
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Computes a topological ordering of the nodes reachable from the given start nodes,
	/// following outgoing edges. A node appears after all of its predecessors (the nodes
	/// whose values it depends on). Lookup reverse edges (flag 4) are ignored for ordering
	/// purposes so that the cyclic lookup connections do not prevent a valid ordering.
	/// </summary>
	/// <param name="startNodes">The indices of the nodes to start from (the user-activated nodes).</param>
	/// <returns>The topologically ordered node indices, or an empty list when the reachable subgraph has a cycle.</returns>
	public List<int> GetTopologicalOrder(IEnumerable<int> startNodes)
	{
		// Build the reachable subgraph (following outgoing edges, ignoring lookup reverse edges).
		HashSet<int> reachable = new HashSet<int>();
		Stack<int> stack = new Stack<int>();
		foreach (int s in startNodes)
		{
			if (s >= 0 && s < this._nodes.Count)
			{
				stack.Push(s);
			}
		}

		while (stack.Count > 0)
		{
			int n = stack.Pop();
			if (!reachable.Add(n))
			{
				continue;
			}

			foreach (int e in this.GetOutgoingEdges(n))
			{
				Edge edge = this.Edges[e];
				if (edge.Flags == 4)
				{
					continue; // skip lookup reverse edges to avoid the cycle
				}

				stack.Push(edge.ToNodeIndex);
			}
		}

		// Kahn's algorithm over the reachable subgraph.
		Dictionary<int, int> inDegree = new Dictionary<int, int>();
		foreach (int n in reachable)
		{
			inDegree[n] = 0;
		}

		foreach (int n in reachable)
		{
			foreach (int e in this.GetOutgoingEdges(n))
			{
				Edge edge = this.Edges[e];
				if (edge.Flags == 4)
				{
					continue;
				}

				if (reachable.Contains(edge.ToNodeIndex))
				{
					inDegree[edge.ToNodeIndex]++;
				}
			}
		}

		Queue<int> queue = new Queue<int>();
		foreach (int n in reachable)
		{
			if (inDegree[n] == 0)
			{
				queue.Enqueue(n);
			}
		}

		List<int> order = new List<int>(reachable.Count);
		while (queue.Count > 0)
		{
			int n = queue.Dequeue();
			order.Add(n);

			foreach (int e in this.GetOutgoingEdges(n))
			{
				Edge edge = this.Edges[e];
				if (edge.Flags == 4)
				{
					continue;
				}

				if (!reachable.Contains(edge.ToNodeIndex))
				{
					continue;
				}

				inDegree[edge.ToNodeIndex]--;
				if (inDegree[edge.ToNodeIndex] == 0)
				{
					queue.Enqueue(edge.ToNodeIndex);
				}
			}
		}

		// If we could not order all reachable nodes, there is a cycle.
		if (order.Count != reachable.Count)
		{
			return new List<int>();
		}

		return order;
	}

	private static bool ListsEqual(IList<int> a, IList<int> b)
	{
		if (a.Count != b.Count)
		{
			return false;
		}

		for (int i = 0; i < a.Count; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}

		return true;
	}
}
