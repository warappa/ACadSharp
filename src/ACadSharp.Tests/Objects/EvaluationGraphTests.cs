using ACadSharp.Objects.Evaluations;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ACadSharp.Tests.Objects;

/// <summary>
/// Tests for the <see cref="EvaluationGraph"/> topological ordering, including the
/// direction-parameterized handling of invertible (flag-4) edge pairs (forward vs.
/// reverse evaluation).
/// </summary>
public class EvaluationGraphTests
{
	/// <summary>
	/// Builds a two-node graph with a single invertible (flag-4) edge pair:
	/// edge 0 (the "forward" direction, the lower index) goes from node 0 to node 1;
	/// edge 1 (the "reverse" direction, the higher index) goes from node 1 to node 0.
	/// The pair mirrors a lookup connection: node 0 is the parameter (the source of the
	/// forward direction), node 1 is the lookup action (the source of the reverse direction).
	/// </summary>
	private static (EvaluationGraph Graph, EvaluationGraph.Node N0, EvaluationGraph.Node N1) buildLookupPair()
	{
		EvaluationGraph graph = new();

		EvaluationGraph.Node n0 = graph.CreateNode();
		EvaluationGraph.Node n1 = graph.CreateNode();
		n0.Index = 0;
		n1.Index = 1;

		// Edge 0 (forward, lower index): node 0 -> node 1.
		EvaluationGraph.Edge e0 = new()
		{
			Index = 0,
			FromNodeIndex = 0,
			ToNodeIndex = 1,
			Flags = EvaluationGraph.EdgeFlags.Invertible,
			ReverseEdge = 1,
			PrevInEdge = -1, NextInEdge = -1,
			PrevOutEdge = -1, NextOutEdge = -1,
		};

		// Edge 1 (reverse, higher index): node 1 -> node 0.
		EvaluationGraph.Edge e1 = new()
		{
			Index = 1,
			FromNodeIndex = 1,
			ToNodeIndex = 0,
			Flags = EvaluationGraph.EdgeFlags.Invertible,
			ReverseEdge = 0,
			PrevInEdge = -1, NextInEdge = -1,
			PrevOutEdge = -1, NextOutEdge = -1,
		};

		graph.Edges.Add(e0);
		graph.Edges.Add(e1);

		// n0: outgoing = edge 0, incoming = edge 1.
		n0.FirstOutEdge = 0;
		n0.LastOutEdge = 0;
		n0.FirstInEdge = 1;
		n0.LastInEdge = 1;

		// n1: incoming = edge 0, outgoing = edge 1.
		n1.FirstInEdge = 0;
		n1.LastInEdge = 0;
		n1.FirstOutEdge = 1;
		n1.LastOutEdge = 1;

		return (graph, n0, n1);
	}

	[Fact]
	public void ForwardOrderFollowsForwardLookupDirectionTest()
	{
		(EvaluationGraph graph, _, _) = buildLookupPair();

		// Forward evaluation (the default): the forward direction of the flag-4 pair is
		// followed (node 0 -> node 1), so starting from node 0 the order is [0, 1].
		List<int> order = graph.GetTopologicalOrder(new[] { 0 });

		Assert.Equal(new[] { 0, 1 }, order);
	}

	[Fact]
	public void ReverseOrderFollowsReverseLookupDirectionTest()
	{
		(EvaluationGraph graph, _, _) = buildLookupPair();

		// Reverse evaluation: the reverse direction of the flag-4 pair is followed
		// (node 1 -> node 0), so starting from node 1 the order is [1, 0].
		List<int> order = graph.GetTopologicalOrder(new[] { 1 }, reverse: true);

		Assert.Equal(new[] { 1, 0 }, order);
	}

	[Fact]
	public void ReverseOrderIgnoresForwardLookupDirectionTest()
	{
		(EvaluationGraph graph, _, _) = buildLookupPair();

		// In reverse mode the forward direction is ignored, so starting from node 0 the
		// only reachable node is node 0 itself (the forward edge 0 is skipped).
		List<int> order = graph.GetTopologicalOrder(new[] { 0 }, reverse: true);

		Assert.Equal(new[] { 0 }, order);
	}

	[Fact]
	public void ForwardOrderIgnoresReverseLookupDirectionTest()
	{
		(EvaluationGraph graph, _, _) = buildLookupPair();

		// In forward mode the reverse direction is ignored, so starting from node 1 the
		// only reachable node is node 1 itself (the reverse edge 1 is skipped).
		List<int> order = graph.GetTopologicalOrder(new[] { 1 });

		Assert.Equal(new[] { 1 }, order);
	}

	[Fact]
	public void IsReverseLookupEdgeTest()
	{
		(EvaluationGraph graph, _, _) = buildLookupPair();

		// Edge 0 (the lower index) is the forward direction; edge 1 (the higher index)
		// is the reverse direction.
		Assert.False(graph.IsReverseLookupEdge(0));
		Assert.True(graph.IsForwardLookupEdge(0));
		Assert.True(graph.IsReverseLookupEdge(1));
		Assert.False(graph.IsForwardLookupEdge(1));
	}

	[Fact]
	public void NonInvertibleEdgeFollowedInBothDirectionsTest()
	{
		// A non-invertible (one-way) edge is followed in both forward and reverse modes.
		EvaluationGraph graph = new();

		EvaluationGraph.Node n0 = graph.CreateNode();
		EvaluationGraph.Node n1 = graph.CreateNode();
		n0.Index = 0;
		n1.Index = 1;

		EvaluationGraph.Edge e0 = new()
		{
			Index = 0,
			FromNodeIndex = 0,
			ToNodeIndex = 1,
			Flags = EvaluationGraph.EdgeFlags.None,
			ReverseEdge = -1,
			PrevInEdge = -1, NextInEdge = -1,
			PrevOutEdge = -1, NextOutEdge = -1,
		};
		graph.Edges.Add(e0);

		// n0: outgoing = edge 0, no incoming.  n1: incoming = edge 0, no outgoing.
		n0.FirstOutEdge = 0;
		n0.LastOutEdge = 0;
		n0.FirstInEdge = -1;
		n0.LastInEdge = -1;
		n1.FirstInEdge = 0;
		n1.LastInEdge = 0;
		n1.FirstOutEdge = -1;
		n1.LastOutEdge = -1;

		// Forward: [0, 1].
		Assert.Equal(new[] { 0, 1 }, graph.GetTopologicalOrder(new[] { 0 }));
		// Reverse: the non-invertible edge is still followed (it is one-way), so [0, 1].
		Assert.Equal(new[] { 0, 1 }, graph.GetTopologicalOrder(new[] { 0 }, reverse: true));
	}

	[Fact]
	public void EvaluateReverseWritesLookupOutputTest()
	{
		// A lookup action (node 1) with a single output column (IsLookupProperty) that
		// writes to node 0's "lookupString" port. The table has one row: input "5" ->
		// output "Matched".
		EvaluationGraph graph = new();

		EvaluationGraph.Node n0 = graph.CreateNode();
		EvaluationGraph.Node n1 = graph.CreateNode();
		n0.Index = 0;
		n1.Index = 1;

		// The flag-4 pair (forward: node 0 -> node 1; reverse: node 1 -> node 0).
		EvaluationGraph.Edge e0 = new()
		{
			Index = 0, FromNodeIndex = 0, ToNodeIndex = 1,
			Flags = EvaluationGraph.EdgeFlags.Invertible, ReverseEdge = 1,
			PrevInEdge = -1, NextInEdge = -1, PrevOutEdge = -1, NextOutEdge = -1,
		};
		EvaluationGraph.Edge e1 = new()
		{
			Index = 1, FromNodeIndex = 1, ToNodeIndex = 0,
			Flags = EvaluationGraph.EdgeFlags.Invertible, ReverseEdge = 0,
			PrevInEdge = -1, NextInEdge = -1, PrevOutEdge = -1, NextOutEdge = -1,
		};
		graph.Edges.Add(e0);
		graph.Edges.Add(e1);

		n0.FirstOutEdge = 0; n0.LastOutEdge = 0;
		n0.FirstInEdge = 1; n0.LastInEdge = 1;
		n1.FirstInEdge = 0; n1.LastInEdge = 0;
		n1.FirstOutEdge = 1; n1.LastOutEdge = 1;

		// Node 0: a text parameter (the "connected parameter").
		n0.Expression = new BlockTextParameter { Id = 1, Value = "original" };

		// Node 1: a lookup action. One input column (a numeric column on node 0's "value"
		// port) and one output column (a text column on node 0's "lookupString" port,
		// IsLookupProperty = true, UnmatchedName = "Default").
		BlockLookupAction.ColumnData input = new()
		{
			NodeId = 1, ValueType = 40, Type = 2, ConnectionName = "value",
		};
		BlockLookupAction.ColumnData output = new()
		{
			NodeId = 1, ValueType = 1, Type = 0, IsLookupProperty = true,
			ConnectionName = "lookupString", UnmatchedName = "Default",
		};
		input.Rows.Add("5");
		output.Rows.Add("Matched");

		BlockLookupAction action = new() { Id = 1 };
		action.Columns = new List<BlockLookupAction.ColumnData> { input, output };
		n1.Expression = action;

		// Activate the lookup action (node 1) and evaluate in the reverse direction:
		// the action is evaluated first (it writes its output to node 0's "lookupString"
		// port), then the parameter is evaluated (it writes the "Value" port).
		graph.Activate(new[] { 1 });
		bool ok = graph.EvaluateReverse();

		Assert.True(ok);

		// The action's output column wrote "Default" (no input match — node 0's "value"
		// port is empty) to node 0's "lookupString" port.
		Assert.True(graph.Context.TryGetValue(1, "lookupString", out EvaluationValue v));
		Assert.Equal("Default", v.StringValue);

		// The action's CurrentValue is the UnmatchedName (a string, since the output
		// column is text).
		Assert.Equal(EvaluationValueType.String, action.CurrentValue.Type);
		Assert.Equal("Default", action.CurrentValue.StringValue);

		// The parameter's "Value" port is still set (the parameter was evaluated after
		// the action).
		Assert.True(graph.Context.TryGetValue(1, "Value", out EvaluationValue p));
		Assert.Equal("original", p.StringValue);
	}

	[Fact]
	public void EvaluateReverseNoActivatedNodesTest()
	{
		// No activated nodes: the evaluation is a no-op (success).
		EvaluationGraph graph = new();
		graph.CreateNode();

		Assert.True(graph.EvaluateReverse());
	}

	[Fact]
	public void EvaluateForwardLookupWriteBackTest()
	{
		// Forward evaluation: the parameter (node 0) is evaluated first (it writes the
		// "value" port), then the lookup action (node 1) is evaluated (it reads the
		// "value" port, matches the row, and writes the matched cell to the output
		// column's port).
		EvaluationGraph graph = new();

		EvaluationGraph.Node n0 = graph.CreateNode();
		EvaluationGraph.Node n1 = graph.CreateNode();
		n0.Index = 0;
		n1.Index = 1;

		EvaluationGraph.Edge e0 = new()
		{
			Index = 0, FromNodeIndex = 0, ToNodeIndex = 1,
			Flags = EvaluationGraph.EdgeFlags.Invertible, ReverseEdge = 1,
			PrevInEdge = -1, NextInEdge = -1, PrevOutEdge = -1, NextOutEdge = -1,
		};
		EvaluationGraph.Edge e1 = new()
		{
			Index = 1, FromNodeIndex = 1, ToNodeIndex = 0,
			Flags = EvaluationGraph.EdgeFlags.Invertible, ReverseEdge = 0,
			PrevInEdge = -1, NextInEdge = -1, PrevOutEdge = -1, NextOutEdge = -1,
		};
		graph.Edges.Add(e0);
		graph.Edges.Add(e1);

		n0.FirstOutEdge = 0; n0.LastOutEdge = 0;
		n0.FirstInEdge = 1; n0.LastInEdge = 1;
		n1.FirstInEdge = 0; n1.LastInEdge = 0;
		n1.FirstOutEdge = 1; n1.LastOutEdge = 1;

		// Node 0: a text parameter that writes the "value" port (a scalar).
		BlockTextParameter param = new() { Id = 1, Value = "5" };
		n0.Expression = param;

		// Node 1: a lookup action. One input column (a text column on node 0's "Value"
		// port) and one output column (a text column on node 0's "lookupString" port,
		// IsLookupProperty = true, UnmatchedName = "Default").
		BlockLookupAction.ColumnData input = new()
		{
			NodeId = 1, ValueType = 1, Type = 0, ConnectionName = "Value",
		};
		BlockLookupAction.ColumnData output = new()
		{
			NodeId = 1, ValueType = 1, Type = 0, IsLookupProperty = true,
			ConnectionName = "lookupString", UnmatchedName = "Default",
		};
		input.Rows.Add("5");
		output.Rows.Add("Matched");

		BlockLookupAction action = new() { Id = 1 };
		action.Columns = new List<BlockLookupAction.ColumnData> { input, output };
		n1.Expression = action;

		// Activate the parameter (node 0) and evaluate in the forward direction:
		// the parameter is evaluated first (it writes the "Value" port = "5"), then the
		// action is evaluated (it reads the "Value" port, matches the row "5", and writes
		// "Matched" to the "lookupString" port).
		graph.Activate(new[] { 0 });
		bool ok = graph.Evaluate();

		Assert.True(ok);

		// The action matched the row (input "5") and wrote "Matched" to node 0's
		// "lookupString" port.
		Assert.True(graph.Context.TryGetValue(1, "lookupString", out EvaluationValue v));
		Assert.Equal("Matched", v.StringValue);

		// The action's CurrentValue is the matched cell.
		Assert.Equal(EvaluationValueType.String, action.CurrentValue.Type);
		Assert.Equal("Matched", action.CurrentValue.StringValue);
	}
}
