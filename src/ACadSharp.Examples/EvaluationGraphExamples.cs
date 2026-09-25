using ACadSharp.IO;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ACadSharp.Examples
{
	/// <summary>
	/// Dumps the full evaluation graph of every dynamic block in a document:
	/// nodes (with the complete expression details), edges, connection
	/// cross-references, and invariant checks on the node/edge data fields.
	/// </summary>
	public class EvaluationGraphExamples
	{
		/// <summary>
		/// Reads the given file (DWG or DXF) and dumps the evaluation graph of every
		/// block record that has one.
		/// </summary>
		public static void DumpEvaluationGraphs(string filePath)
		{
			CadDocument doc = ReadFile(filePath);

			List<(string BlockName, EvaluationGraph Graph)> graphs = doc.BlockRecords
				.Where(b => b.EvaluationGraph != null)
				.Select(b => (b.Name, b.EvaluationGraph))
				.ToList();

			Console.WriteLine($"Found {graphs.Count} block record(s) with an evaluation graph in {Path.GetFileName(filePath)}");

			foreach ((string blockName, EvaluationGraph graph) in graphs)
			{
				DumpGraph(blockName, graph);
			}
		}

		private static CadDocument ReadFile(string filePath)
		{
			string ext = Path.GetExtension(filePath).ToLowerInvariant();
			if (ext == ".dxf")
			{
				using (DxfReader reader = new DxfReader(filePath))
				{
					return reader.Read();
				}
			}
			else
			{
				using (DwgReader reader = new DwgReader(filePath))
				{
					return reader.Read();
				}
			}
		}

		private static void DumpGraph(string blockName, EvaluationGraph graph)
		{
			Console.WriteLine();
			Console.WriteLine($"=== Block '{blockName}' ===");
			Console.WriteLine($"96={graph.Value96} 97={graph.Value97} nodes={graph.Nodes.Count()} edges={graph.Edges.Count}");

			Dictionary<int, EvaluationGraph.Node> idToNode = graph.Nodes.ToDictionary(n => n.Id);
			Dictionary<int, EvaluationGraph.Node> indexToNode = graph.Nodes.ToDictionary(n => n.Index);

			Console.WriteLine();
			Console.WriteLine("--- nodes ---");
			foreach (EvaluationGraph.Node node in graph.Nodes.OrderBy(n => n.Index))
			{
				Console.WriteLine($"node {node.Index,2} (id {node.Id,2}, flags {node.Flags} [0x{(int)node.Flags:X2}]): {DumpExpression(node.Expression, idToNode)}");
			}

			Console.WriteLine();
			Console.WriteLine("--- edges ---");
			foreach (EvaluationGraph.Edge edge in graph.Edges.OrderBy(e => e.Index))
			{
				Console.WriteLine($"E{edge.Index,2} {edge.FromNodeIndex,2}->{edge.ToNodeIndex,2}  flags={edge.Flags} tracked={edge.TrackedCount}  data=({edge.Data1},{edge.Data2},{edge.Data3},{edge.Data4},{edge.Data5})");
			}

			// invariant checks
			int nodeFail = 0, edgeFail = 0, trackedFail = 0;
			foreach (EvaluationGraph.Node node in graph.Nodes)
			{
				List<int> ins = graph.Edges.Where(e => e.ToNodeIndex == node.Index).Select(e => e.Index).ToList();
				List<int> outs = graph.Edges.Where(e => e.FromNodeIndex == node.Index).Select(e => e.Index).ToList();
				(int a, int b, int c, int d) expected = (ins.Count == 0 ? -1 : ins[0], ins.Count == 0 ? -1 : ins[^1], outs.Count == 0 ? -1 : outs[0], outs.Count == 0 ? -1 : outs[^1]);
				if (node.Data1 != expected.a || node.Data2 != expected.b || node.Data3 != expected.c || node.Data4 != expected.d)
				{
					nodeFail++;
					Console.WriteLine($"  NODE MISMATCH {node.Index}: data=({node.Data1},{node.Data2},{node.Data3},{node.Data4}) expected={expected}");
				}
			}
			foreach (EvaluationGraph.Edge edge in graph.Edges)
			{
				List<int> ins = graph.Edges.Where(e => e.ToNodeIndex == edge.ToNodeIndex).Select(e => e.Index).ToList();
				List<int> outs = graph.Edges.Where(e => e.FromNodeIndex == edge.FromNodeIndex).Select(e => e.Index).ToList();
				int pi = ins.IndexOf(edge.Index);
				int po = outs.IndexOf(edge.Index);
				(int a, int b, int c, int d) expected = (pi > 0 ? ins[pi - 1] : -1, pi + 1 < ins.Count ? ins[pi + 1] : -1, po > 0 ? outs[po - 1] : -1, po + 1 < outs.Count ? outs[po + 1] : -1);
				if (edge.Data1 != expected.a || edge.Data2 != expected.b || edge.Data3 != expected.c || edge.Data4 != expected.d)
				{
					edgeFail++;
					Console.WriteLine($"  EDGE MISMATCH {edge.Index}: data=({edge.Data1},{edge.Data2},{edge.Data3},{edge.Data4},{edge.Data5}) expected prev/next={expected}");
				}
				// hypothesis: TrackedCount = number of connections on the TO element pointing at the FROM element
				if (indexToNode.TryGetValue(edge.FromNodeIndex, out EvaluationGraph.Node fromNode) &&
					indexToNode.TryGetValue(edge.ToNodeIndex, out EvaluationGraph.Node toNode) &&
					fromNode.Expression != null && toNode.Expression != null)
				{
					List<(int Id, string Name)> toConns = GetConnections(toNode.Expression);
					int count = toConns.Count(c => c.Id == fromNode.Expression.Id);
					if (count != edge.TrackedCount)
					{
						trackedFail++;
						Console.WriteLine($"  TRACKED MISMATCH E{edge.Index} ({edge.FromNodeIndex}->{edge.ToNodeIndex}): tracked={edge.TrackedCount} conns on TO pointing at FROM={count}");
					}
				}
			}
			Console.WriteLine($"invariants: node data {(nodeFail == 0 ? "OK" : $"{nodeFail} FAIL")}  edge data {(edgeFail == 0 ? "OK" : $"{edgeFail} FAIL")}  trackedCount-hyp {(trackedFail == 0 ? "OK" : $"{trackedFail} FAIL")}");
		}

		/// <summary>
		/// Collects all (targetId, portName) connection entries of an expression.
		/// </summary>
		private static List<(int Id, string Name)> GetConnections(EvaluationExpression expr)
		{
			List<(int Id, string Name)> result = new();
			void Add(EvalConnection c)
			{
				if (c != null && c.Id != 0)
				{
					result.Add((c.Id, c.Name));
				}
			}
			void AddProp(EvalParameterProperty p)
			{
				if (p != null)
				{
					foreach (EvalConnection c in p.Connections)
					{
						Add(c);
					}
				}
			}
			switch (expr)
			{
				case Block1PtParameter p:
					AddProp(p.DisplacementX);
					AddProp(p.DisplacementY);
					break;
				case Block2PtParameter p:
					AddProp(p.FirstPointDisplacementX);
					AddProp(p.FirstPointDisplacementY);
					AddProp(p.SecondPointDisplacementX);
					AddProp(p.SecondPointDisplacementY);
					break;
				case BlockGripLocationComponent c:
					Add(c.Connection);
					break;
				case BlockScaleAction a:
					Add(a.ScaleConnection);
					Add(a.XScaleConnection);
					Add(a.YScaleConnection);
					Add(a.UpdateBaseXConnection);
					Add(a.UpdateBaseYConnection);
					break;
				case BlockMoveAction a:
					Add(a.XDeltaConnection);
					Add(a.YDeltaConnection);
					break;
				case BlockRotationAction a:
					Add(a.AngleDeltaConnection);
					Add(a.UpdateBaseXConnection);
					Add(a.UpdateBaseYConnection);
					break;
				case BlockStretchAction a:
					Add(a.EndXDeltaConnection);
					Add(a.EndYDeltaConnection);
					break;
				case BlockPolarStretchAction a:
					Add(a.BaseConnection);
					Add(a.BaseXDeltaConnection);
					Add(a.BaseYDeltaConnection);
					Add(a.EndConnection);
					Add(a.UpdatedBaseConnection);
					Add(a.UpdatedEndConnection);
					break;
				case BlockArrayAction a:
					Add(a.BaseConnection);
					Add(a.EndConnection);
					Add(a.UpdatedBaseConnection);
					Add(a.UpdatedEndConnection);
					break;
				case BlockFlipAction a:
					Add(a.FlipConnection);
					Add(a.UpdatedBaseConnection);
					Add(a.UpdatedEndConnection);
					Add(a.UpdatedFlipConnection);
					break;
				case BlockFlipParameter p:
					Add(p.UpdatedFlipConnection);
					break;
			}
			return result;
		}

		private static string DumpExpression(EvaluationExpression expr, Dictionary<int, EvaluationGraph.Node> idToNode)
		{
			if (expr == null)
			{
				return "<no expression>";
			}

			string ev = expr.EvaluatedValue == null ? "-" : $"{(int)expr.EvaluatedValue.Code}:{expr.EvaluatedValue.Value}";
			string head = $"{expr.GetType().Name:28s} id={expr.Id} 98={expr.Value98} 99={expr.Value99} evaluated={ev}";

			switch (expr)
			{
				case BlockLinearParameter p:
					return $"{head} | label={p.Label} desc={p.Description} 140={p.LabelOffset} 1010={Fmt(p.FirstPoint)} 1011={Fmt(p.SecondPoint)} baseLoc={p.BaseLocation} gripIds=[{string.Join(",", p.GripIds)}] valueSet={DumpValueSet(p.ValueSet)} conns={DumpProps(p.FirstPointDisplacementX, p.FirstPointDisplacementY, p.SecondPointDisplacementX, p.SecondPointDisplacementY)}";

				case BlockPointParameter p:
					return $"{head} | label={p.Label} desc={p.Description} 1010={Fmt(p.Location)} gripId={p.GripId} conns={DumpProps(p.DisplacementX, p.DisplacementY)}";

				case BlockRotationParameter p:
					return $"{head} | label={p.Label} desc={p.Description} 140={p.LabelOffset} 1010={Fmt(p.FirstPoint)} 1011={Fmt(p.SecondPoint)} gripIds=[{string.Join(",", p.GripIds)}] valueSet={DumpValueSet(p.ValueSet)} conns={DumpProps(p.FirstPointDisplacementX, p.FirstPointDisplacementY, p.SecondPointDisplacementX, p.SecondPointDisplacementY)}";

				case BlockPolarParameter p:
					return $"{head} | label={p.Label} angle={p.AngleName}/{p.AngleDescription} 140={p.LabelOffset} 1010={Fmt(p.FirstPoint)} 1011={Fmt(p.SecondPoint)} gripIds=[{string.Join(",", p.GripIds)}] dist={DumpValueSet(p.DistanceValueSet)} angle={DumpValueSet(p.AngleValueSet)} conns={DumpProps(p.FirstPointDisplacementX, p.FirstPointDisplacementY, p.SecondPointDisplacementX, p.SecondPointDisplacementY)}";

				case BlockXYParameter p:
					return $"{head} | labelX={p.LabelX} labelY={p.LabelY} 140={p.LabelOffsetY} 141={p.LabelOffsetX} 1010={Fmt(p.FirstPoint)} 1011={Fmt(p.SecondPoint)} gripIds=[{string.Join(",", p.GripIds)}] conns={DumpProps(p.FirstPointDisplacementX, p.FirstPointDisplacementY, p.SecondPointDisplacementX, p.SecondPointDisplacementY)}";

				case BlockAlignmentParameter p:
					return $"{head} | 280={p.IsPerpendicular} 1010={Fmt(p.FirstPoint)} 1011={Fmt(p.SecondPoint)} gripIds=[{string.Join(",", p.GripIds)}] conns={DumpProps(p.FirstPointDisplacementX, p.FirstPointDisplacementY, p.SecondPointDisplacementX, p.SecondPointDisplacementY)}";

				case BlockFlipParameter p:
					return $"{head} | base={p.BaseStateName} flipped={p.FlippedStateName} desc={p.Description} label={p.Label} 1010={Fmt(p.FirstPoint)} 1011={Fmt(p.SecondPoint)} 1012={Fmt(p.LabelPosition)} gripIds=[{string.Join(",", p.GripIds)}] conns={DumpProps(p.FirstPointDisplacementX, p.FirstPointDisplacementY, p.SecondPointDisplacementX, p.SecondPointDisplacementY, p.UpdatedFlipConnection)}";

				case BlockBasePointParameter p:
					return $"{head} | 1010={Fmt(p.Location)} 1011={Fmt(p.Point1011)} 1012={Fmt(p.Point1012)} gripId={p.GripId} conns={DumpProps(p.DisplacementX, p.DisplacementY)}";

				case BlockLookupParameter p:
					return $"{head} | label={p.Label} desc={p.Description} 94={p.ActionId} 1010={Fmt(p.Location)} gripId={p.GripId} conns={DumpProps(p.DisplacementX, p.DisplacementY)}";

				case BlockVisibilityParameter p:
					return $"{head} | label={p.Label} desc={p.Description} 91={p.Value91} 281={p.Value281} 1010={Fmt(p.Location)} gripId={p.GripId} states=[{string.Join(", ", p.States.Keys)}] entities={p.Entities.Count}";

				case BlockLinearGrip g:
					return $"{head} | location={Fmt(g.Location)} 280={g.Cycling} 140={g.DistanceX} 141={g.DistanceY} 142={g.DistanceZ} 91={g.ExpressionId1} 92={g.ExpressionId2} 93={g.Value93}";

				case BlockFlipGrip g:
					return $"{head} | location={Fmt(g.Location)} 280={g.Cycling} 140={g.DirectionX} 141={g.DirectionY} 142={g.DirectionZ} 91={g.ExpressionId1} 92={g.ExpressionId2} 93={g.FlipExpressionId}";

				case BlockAlignmentGrip g:
					return $"{head} | location={Fmt(g.Location)} 280={g.Cycling} 140={g.AlignmentX} 141={g.AlignmentY} 142={g.AlignmentZ} 91={g.ExpressionId1} 92={g.ExpressionId2} 93={g.Value93}";

				case BlockGrip g:
					return $"{head} | location={Fmt(g.Location)} 280={g.Cycling} 91={g.ExpressionId1} 92={g.ExpressionId2} 93={g.Value93}";

				case BlockGripLocationComponent c:
					return $"{head} | conn={DumpConn(c.Connection, idToNode)}";

				case BlockScaleAction a:
					return $"{head} | scaleType={a.ScaleType} 1010={Fmt(a.LabelPosition)} 1011={Fmt(a.BasePoint)} 1012={Fmt(a.Value1012)} 280={a.Value280} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] scale={DumpConn(a.ScaleConnection, idToNode)} xScale={DumpConn(a.XScaleConnection, idToNode)} yScale={DumpConn(a.YScaleConnection, idToNode)} baseX={DumpConn(a.UpdateBaseXConnection, idToNode)} baseY={DumpConn(a.UpdateBaseYConnection, idToNode)}";

				case BlockRotationAction a:
					return $"{head} | 1010={Fmt(a.LabelPosition)} 1011={Fmt(a.BasePoint)} 1012={Fmt(a.Value1012)} 280={a.Value280} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] angle={DumpConn(a.AngleDeltaConnection, idToNode)} baseX={DumpConn(a.UpdateBaseXConnection, idToNode)} baseY={DumpConn(a.UpdateBaseYConnection, idToNode)}";

				case BlockMoveAction a:
					return $"{head} | 140={a.DistanceMultiplier} 141={a.AngleOffset} 280={a.UnknownFlag} 1010={Fmt(a.LabelPosition)} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] x={DumpConn(a.XDeltaConnection, idToNode)} y={DumpConn(a.YDeltaConnection, idToNode)}";

				case BlockStretchAction a:
					return $"{head} | 140={a.DistanceMultiplier} 141={a.AngleOffset} 280={a.UnknownFlag} 1010={Fmt(a.LabelPosition)} boundary={a.Boundary.Count} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] endX={DumpConn(a.EndXDeltaConnection, idToNode)} endY={DumpConn(a.EndYDeltaConnection, idToNode)}";

				case BlockPolarStretchAction a:
					return $"{head} | 140={a.AngleOffset} 141={a.DistanceMultiplier} 1010={Fmt(a.LabelPosition)} boundary={a.Boundary.Count} rotate={a.RotateBindings.Count} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] base={DumpConn(a.BaseConnection, idToNode)} baseX={DumpConn(a.BaseXDeltaConnection, idToNode)} baseY={DumpConn(a.BaseYDeltaConnection, idToNode)} end={DumpConn(a.EndConnection, idToNode)} updatedBase={DumpConn(a.UpdatedBaseConnection, idToNode)} updatedEnd={DumpConn(a.UpdatedEndConnection, idToNode)}";

				case BlockArrayAction a:
					return $"{head} | 140={a.RowOffset} 141={a.ColumnOffset} 1010={Fmt(a.LabelPosition)} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] base={DumpConn(a.BaseConnection, idToNode)} end={DumpConn(a.EndConnection, idToNode)} updatedBase={DumpConn(a.UpdatedBaseConnection, idToNode)} updatedEnd={DumpConn(a.UpdatedEndConnection, idToNode)}";

				case BlockLookupAction a:
					return $"{head} | 280={a.UnknownFlag} 1010={Fmt(a.LabelPosition)} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] columns={a.Columns.Count}";

				case BlockFlipAction a:
					return $"{head} | 1010={Fmt(a.LabelPosition)} entities={a.Entities.Count} params=[{string.Join(",", a.ParametersIds)}] flip={DumpConn(a.FlipConnection, idToNode)} base={DumpConn(a.UpdatedBaseConnection, idToNode)} end={DumpConn(a.UpdatedEndConnection, idToNode)} updatedFlip={DumpConn(a.UpdatedFlipConnection, idToNode)}";

				default:
					return $"{head} | <{expr.GetType().Name}>";
			}
		}

		private static string Fmt(CSMath.XYZ p)
		{
			return $"({p.X:0.###},{p.Y:0.###},{p.Z:0.###})";
		}

		private static string DumpValueSet(ParameterValueSet v)
		{
			if (v == null)
			{
				return "-";
			}
			return $"[{v.Type}: allowed={v.AllowedValues.Count} inc={v.Increment:0.###} min={v.Minimum:0.###} max={v.Maximum:0.###}]";
		}

		private static string DumpConn(EvalConnection c, Dictionary<int, EvaluationGraph.Node> idToNode)
		{
			if (c == null || (c.Id == 0 && string.IsNullOrEmpty(c.Name)))
			{
				return "-";
			}
			string target = "-";
			if (idToNode.TryGetValue(c.Id, out EvaluationGraph.Node node) && node.Expression != null)
			{
				target = $"->node{node.Index}({node.Expression.GetType().Name})";
			}
			return $"{c.Id}:{c.Name} {target}";
		}

		private static string DumpProps(params object[] items)
		{
			// alternate: EvalParameterProperty or EvalConnection
			string result = "[";
			for (int i = 0; i < items.Length; i++)
			{
				if (i > 0)
				{
					result += " ";
				}
				result += items[i] switch
				{
					EvalParameterProperty p => $"({string.Join(",", p.Connections.Select(c => $"{c.Id}:{c.Name}"))})",
					EvalConnection c => $"{c.Id}:{c.Name}",
					_ => "?",
				};
			}
			return result + "]";
		}
	}
}
