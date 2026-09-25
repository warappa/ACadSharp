using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Tests.TestModels;
using CSMath;
using CSMath.Extensions;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ACadSharp.Tests.IO;

/// <summary>
/// Tests the evaluation engine end-to-end: activates a grip with a known
/// <see cref="BlockGrip.ActivatedLocation"/>, evaluates the graph, and verifies the
/// computed parameter values against the geometry.
/// </summary>
public class EvaluationTests : IOTestsBase
{
	public static TheoryData<FileModel> Samples { get; } = new();

	static EvaluationTests()
	{
		loadSamples("./dynamic-blocks", "*dxf", Samples);
	}

	public EvaluationTests(ITestOutputHelper output) : base(output)
	{
	}

	/// <summary>
	/// Loads the given sample (DXF) and returns the block record with an evaluation graph.
	/// </summary>
	private (CadDocument Doc, BlockRecord Block, EvaluationGraph Graph) loadGraph(string sampleName)
	{
		string path = Path.Combine(TestVariables.SamplesFolder, "dynamic-blocks", $"{sampleName}.dxf");
		using (DxfReader reader = new DxfReader(path))
		{
			CadDocument doc = reader.Read();
			BlockRecord block = doc.BlockRecords
				.First(b => b.EvaluationGraph != null && b.EvaluationGraph.Nodes.Count() > 0);
			return (doc, block, block.EvaluationGraph);
		}
	}

	/// <summary>
	/// Finds the index of the node that holds the given expression.
	/// </summary>
	private static int nodeIndex(EvaluationGraph graph, EvaluationExpression expr)
	{
		return graph.Nodes.First(n => n.Expression == expr).Index;
	}

	/// <summary>
	/// Compares two points with a tolerance.
	/// </summary>
	private static bool close(XYZ a, XYZ b, double tolerance = 1e-3)
	{
		return (a - b).GetLength() < tolerance;
	}

	/// <summary>
	/// Asserts that a raw double is equal to the expected value within a tolerance.
	/// </summary>
	private static void assertClose(double expected, double actual, double tolerance = 1e-3)
	{
		Assert.True(Math.Abs(expected - actual) < tolerance, $"Expected {expected}, got {actual} (tolerance {tolerance}).");
	}

	/// <summary>
	/// Asserts that a typed <see cref="EvaluationValue{double}"/> holds the expected value within a tolerance.
	/// </summary>
	private static void assertCloseEval(double expected, EvaluationValue<double> actual, double tolerance = 1e-3)
	{
		Assert.True(actual.IsSet, "The value is not set.");
		assertClose(expected, actual.Value, tolerance);
	}

	/// <summary>
	/// Asserts that a typed <see cref="EvaluationValue{XYZ}"/> holds the expected point within a tolerance.
	/// </summary>
	private static void assertPointClose(XYZ expected, EvaluationValue<XYZ> actual, double tolerance = 1e-3)
	{
		Assert.True(actual.IsSet, "The value is not set.");
		Assert.True((actual.Value - expected).GetLength() < tolerance, $"Expected {expected}, got {actual.Value} (tolerance {tolerance}).");
	}

	/// <summary>
	/// Linear parameter: move the end grip by a known displacement and verify the
	/// computed distance.
	/// </summary>
	[Fact]
	public void EvaluateLinearParameterTest()
	{
		(_, BlockRecord block, EvaluationGraph graph) = this.loadGraph("BLOCKLINEARPARAMETER");

		BlockLinearParameter param = block.EvaluationGraph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockLinearParameter>()
			.First();

		// The base point is at (0,0,0) and the end point is at (5,0,0): the initial
		// distance is 5.
		XYZ basePoint = param.FirstPoint;
		XYZ endPoint = param.SecondPoint;
		double initialDistance = (endPoint - basePoint).GetLength();
		assertClose(5.0, initialDistance);

		// Find the end grip (the one at the end point) and move it by (3,0,0).
		BlockGrip endGrip = graph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockLinearGrip>()
			.First(g => close(g.Location, endPoint));

		endGrip.ActivatedLocation = endPoint + new XYZ(3, 0, 0);

		graph.Activate(new[] { nodeIndex(graph, endGrip) });
		Assert.True(graph.Evaluate(), "The evaluation failed.");

		// The new distance should be 8 (the end point moved by 3 along the axis).
		assertCloseEval(8.0, param.CurrentValue);
	}

	/// <summary>
	/// Polar parameter: move the end grip radially and verify the computed distance.
	/// </summary>
	[Fact]
	public void EvaluatePolarParameterTest()
	{
		(_, BlockRecord block, EvaluationGraph graph) = this.loadGraph("BLOCKPOLARPARAMETER");

		BlockPolarParameter param = block.EvaluationGraph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockPolarParameter>()
			.First();

		// The base point is at (2.007, 2.346, 0) and the end point is at (8.429, 8.767, 0):
		// the initial distance is ~9.08.
		XYZ basePoint = param.FirstPoint;
		XYZ endPoint = param.SecondPoint;
		double initialDistance = (endPoint - basePoint).GetLength();
		assertClose(9.08, initialDistance, 0.01);

		// Find the end grip (the one NOT at the base point) and move it by (2,0,0).
		BlockGrip endGrip = graph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockPolarGrip>()
			.First(g => !close(g.Location, basePoint));

		endGrip.ActivatedLocation = endGrip.Location + new XYZ(2, 0, 0);

		graph.Activate(new[] { nodeIndex(graph, endGrip) });
		Assert.True(graph.Evaluate(), "The evaluation failed.");

		// The end point moved by (2,0,0): the new distance = sqrt((8.429+2-2.007)^2 + (8.767-2.346)^2).
		// The polar parameter's CurrentValue is the (distance, angle) pair as a polar-space point
		// (X = distance, Y = angle), so the distance is the X component.
		XYZ newEnd = endPoint + new XYZ(2, 0, 0);
		double newDistance = (newEnd - basePoint).GetLength();
		assertClose(newDistance, param.CurrentValue.Value.X, 0.01);
	}

	/// <summary>
	/// Rotation parameter: rotate the grip and verify the computed angle.
	/// </summary>
	[Fact]
	public void EvaluateRotationParameterTest()
	{
		(_, BlockRecord block, EvaluationGraph graph) = this.loadGraph("BLOCKROTATIONPARAMETER");

		BlockRotationParameter param = block.EvaluationGraph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockRotationParameter>()
			.First();

		// The base point is at (0,0,0) and the end point is at (0,5,0): the initial angle
		// is 90 degrees (1.571 rad).
		XYZ basePoint = param.FirstPoint;
		XYZ endPoint = param.SecondPoint;
		double initialAngle = Math.Atan2(endPoint.Y - basePoint.Y, endPoint.X - basePoint.X);
		assertClose(1.571, initialAngle);

		// Find the end grip and rotate it by 45 degrees (0.785 rad).
		BlockGrip endGrip = graph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockRotationGrip>()
			.First(g => close(g.Location, endPoint));

		// Rotate the end point by 45 degrees around the base point.
		double angle = 0.785;
		double dx = endPoint.X - basePoint.X;
		double dy = endPoint.Y - basePoint.Y;
		XYZ rotated = new XYZ(
			basePoint.X + dx * Math.Cos(angle) - dy * Math.Sin(angle),
			basePoint.Y + dx * Math.Sin(angle) + dy * Math.Cos(angle),
			endPoint.Z);
		endGrip.ActivatedLocation = rotated;

		graph.Activate(new[] { nodeIndex(graph, endGrip) });
		Assert.True(graph.Evaluate(), "The evaluation failed.");

		// The new angle should be 135 degrees (2.356 rad).
		assertCloseEval(2.356, param.CurrentValue);
	}

	/// <summary>
	/// Point parameter: move the grip and verify the computed displacement.
	/// </summary>
	[Fact]
	public void EvaluatePointParameterTest()
	{
		(_, BlockRecord block, EvaluationGraph graph) = this.loadGraph("BLOCKPOINTPARAMETER");

		BlockPointParameter param = block.EvaluationGraph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockPointParameter>()
			.First();

		// The initial displacement is 0 (the grip is not moved).
		XYZ location = param.Location;

		// Find the grip and move it by (2,3,0).
		BlockGrip grip = graph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockXYGrip>()
			.First(g => close(g.Location, location));

		grip.ActivatedLocation = location + new XYZ(2, 3, 0);

		graph.Activate(new[] { nodeIndex(graph, grip) });
		Assert.True(graph.Evaluate(), "The evaluation failed.");

		// The point parameter's CurrentValue is the full (X, Y) displacement (2, 3, 0) —
		// not just the X component.
		assertPointClose(new XYZ(2, 3, 0), param.CurrentValue);
	}

	/// <summary>
	/// Verify that evaluating with no activated grips produces zero displacements (the
	/// initial state).
	/// </summary>
	[Fact]
	public void EvaluateNoGripsTest()
	{
		(_, BlockRecord block, EvaluationGraph graph) = this.loadGraph("BLOCKLINEARPARAMETER");

		// Activate no grips: the evaluation should be a no-op (return true), because the
		// reachable subgraph is empty.
		graph.Activate(Array.Empty<int>());
		Assert.True(graph.Evaluate(), "The evaluation should be a no-op when no grips are activated.");
	}

	/// <summary>
	/// Verify that a component's stored <see cref="EvaluationExpression.EvaluatedValue"/>
	/// (code 40) is updated after evaluation: before evaluation it is the "not yet
	/// evaluated" sentinel, and after evaluation it is the computed value.
	/// </summary>
	[Fact]
	public void EvaluateUpdatesEvaluatedValueTest()
	{
		(_, BlockRecord block, EvaluationGraph graph) = this.loadGraph("BLOCKLINEARPARAMETER");

		// Find the component that mirrors the end point's X coordinate (the "UpdatedEndX"
		// port of the linear parameter).
		BlockGripLocationComponent component = graph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockGripLocationComponent>()
			.First(c => c.Connection.Name == "UpdatedEndX");

		// Before evaluation, the component's EvaluatedValue is the "not yet evaluated"
		// sentinel (1.797693134862314E+99) or null.
		double? initialValue = component.EvaluatedValue?.Value as double?;
		bool isSentinel = initialValue.HasValue && Math.Abs(initialValue.Value - 1.797693134862314E+99) < 1;
		Assert.True(isSentinel || initialValue == null, $"The initial EvaluatedValue should be the sentinel or null, got {initialValue}.");

		// Activate the end grip (move it by (3,0,0)) and evaluate.
		BlockLinearParameter param = graph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockLinearParameter>()
			.First();
		XYZ endPoint = param.SecondPoint;
		BlockGrip endGrip = graph.Nodes
			.Select(n => n.Expression)
			.OfType<BlockLinearGrip>()
			.First(g => close(g.Location, endPoint));
		endGrip.ActivatedLocation = endPoint + new XYZ(3, 0, 0);

		graph.Activate(new[] { nodeIndex(graph, endGrip) });
		Assert.True(graph.Evaluate(), "The evaluation failed.");

		// After evaluation, the component's EvaluatedValue should be the computed value
		// (the updated end point's X coordinate = 8).
		Assert.NotNull(component.EvaluatedValue);
		double evalValue = (double)component.EvaluatedValue.Value;
		assertClose(8.0, evalValue);
	}
}
