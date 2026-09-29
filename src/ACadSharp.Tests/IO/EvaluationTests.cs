using ACadSharp.IO;
using ACadSharp.Objects;
using ACadSharp.Objects.Evaluations;
using ACadSharp.Tables;
using ACadSharp.Tests.TestModels;
using CSMath;
using CSMath.Extensions;
using System;
using System.Collections.Generic;
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

	/// <summary>
	/// Horizontal constraint parameter: the value is the horizontal (X) distance from the
	/// base point to the end point. With no activated grips the value is the stored distance.
	/// </summary>
	[Fact]
	public void EvaluateHorizontalConstraintParameterTest()
	{
		BlockHorizontalConstraintParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(0, 0, 0),
			SecondPoint = new XYZ(5, 0, 0),
		};

		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.True(param.CurrentValue.IsSet, "The value is not set.");
		assertClose(5.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// Vertical constraint parameter: the value is the vertical (Y) distance from the base
	/// point to the end point.
	/// </summary>
	[Fact]
	public void EvaluateVerticalConstraintParameterTest()
	{
		BlockVerticalConstraintParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(0, 0, 0),
			SecondPoint = new XYZ(0, 8, 0),
		};

		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.True(param.CurrentValue.IsSet, "The value is not set.");
		assertClose(8.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// User parameter: the value is the stored double (the user's input).
	/// </summary>
	[Fact]
	public void EvaluateUserParameterTest()
	{
		BlockUserParameter param = new()
		{
			Id = 1,
			Value = 42.0,
		};

		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.True(param.CurrentValue.IsSet, "The value is not set.");
		assertClose(42.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// User parameter (not evaluated): the <see cref="BlockUserParameter.CurrentValue"/> falls
	/// back to the stored <see cref="BlockUserParameter.Value"/> (the user's last input) rather
	/// than an empty placeholder, so the parameter always exposes its current value.
	/// </summary>
	[Fact]
	public void UserParameterCurrentValueFallsBackToStoredValueTest()
	{
		BlockUserParameter param = new()
		{
			Id = 1,
			Value = 42.0,
		};

		// Do NOT evaluate: the CurrentValue should still be set (to the stored value).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored value.");
		assertClose(42.0, param.CurrentValue.Value);

		// After evaluation, the CurrentValue is the evaluated value (the same as the stored value).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(42.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// Character parameter (not evaluated): the <see cref="BlockCharParameter.CurrentValue"/> falls
	/// back to the stored <see cref="BlockCharParameter.Value"/> (the held character) rather than an
	/// empty placeholder.
	/// </summary>
	[Fact]
	public void CharParameterCurrentValueFallsBackToStoredValueTest()
	{
		BlockCharParameter param = new()
		{
			Id = 1,
			Value = 'A',
		};

		// Do NOT evaluate: the CurrentValue should still be set (to the stored character).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored character.");
		Assert.Equal('A', param.CurrentValue.Value);

		// After evaluation, the CurrentValue is the evaluated value (the same as the stored character).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.Equal('A', param.CurrentValue.Value);
	}

	/// <summary>
	/// Text parameter (not evaluated): the <see cref="BlockTextParameter.CurrentValue"/> falls
	/// back to the stored <see cref="BlockTextParameter.Value"/> (the held text) rather than an
	/// empty placeholder.
	/// </summary>
	[Fact]
	public void TextParameterCurrentValueFallsBackToStoredValueTest()
	{
		BlockTextParameter param = new()
		{
			Id = 1,
			Value = "hello",
		};

		// Do NOT evaluate: the CurrentValue should still be set (to the stored text).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored text.");
		Assert.Equal("hello", param.CurrentValue.Value);

		// After evaluation, the CurrentValue is the evaluated value (the same as the stored text).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.Equal("hello", param.CurrentValue.Value);
	}

	/// <summary>
	/// Handle parameter (not evaluated): the <see cref="BlockHandleParameter.CurrentValue"/> falls
	/// back to the stored <see cref="BlockHandleParameter.Value"/> (the held object handle) rather
	/// than an empty placeholder.
	/// </summary>
	[Fact]
	public void HandleParameterCurrentValueFallsBackToStoredValueTest()
	{
		BlockHandleParameter param = new()
		{
			Id = 1,
			Value = 12345,
		};

		// Do NOT evaluate: the CurrentValue should still be set (to the stored handle).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored handle.");
		Assert.Equal(12345L, param.CurrentValue.Value);

		// After evaluation, the CurrentValue is the evaluated value (the same as the stored handle).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.Equal(12345L, param.CurrentValue.Value);
	}

	/// <summary>
	/// Grip (not evaluated, not activated): the <see cref="BlockGrip.CurrentValue"/> falls back to
	/// the <see cref="BlockGrip.Displacement"/> (zero) rather than an empty placeholder. After the
	/// grip is activated and evaluated, the CurrentValue is the displacement.
	/// </summary>
	[Fact]
	public void GripCurrentValueFallsBackToZeroDisplacementTest()
	{
		BlockLinearGrip grip = new()
		{
			Id = 1,
			Location = new XYZ(1, 2, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to the displacement (zero).
		Assert.True(grip.CurrentValue.IsSet, "The value should fall back to the displacement.");
		assertClose(0.0, grip.CurrentValue.Value.X);
		assertClose(0.0, grip.CurrentValue.Value.Y);
		assertClose(0.0, grip.CurrentValue.Value.Z);

		// After activation + evaluation, the CurrentValue is the displacement (0, 0, 5).
		grip.ActivatedLocation = new XYZ(1, 2, 5);
		Assert.True(grip.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(0.0, grip.CurrentValue.Value.X);
		assertClose(0.0, grip.CurrentValue.Value.Y);
		assertClose(5.0, grip.CurrentValue.Value.Z);
	}

	/// <summary>
	/// The base (untyped) <see cref="EvaluationExpression.CurrentValue"/> falls back to the node's
	/// default when the node has not been evaluated yet. This is what the node viewer's
	/// <c>ValueFormatter</c> reads (via an <see cref="EvaluationExpression"/> reference), so an
	/// unevaluated node exposes its default value rather than an empty placeholder.
	/// </summary>
	[Fact]
	public void BaseCurrentValueFallsBackToDefaultTest()
	{
		// A user parameter (not evaluated): the base CurrentValue falls back to the stored value.
		EvaluationExpression userParam = new BlockUserParameter { Id = 1, Value = 42.0 };
		EvaluationValue userBase = userParam.CurrentValue;
		Assert.Equal(EvaluationValueType.Double, userBase.Type);
		assertClose(42.0, userBase.DoubleValue ?? 0.0);

		// A grip (not evaluated, not activated): the base CurrentValue falls back to zero.
		EvaluationExpression grip = new BlockLinearGrip { Id = 2, Location = new XYZ(1, 2, 0) };
		EvaluationValue gripBase = grip.CurrentValue;
		Assert.Equal(EvaluationValueType.Point, gripBase.Type);
		XYZ p = gripBase.PointValue ?? XYZ.Zero;
		assertClose(0.0, p.X);
		assertClose(0.0, p.Y);
		assertClose(0.0, p.Z);
	}

	/// <summary>
	/// XY parameter (not evaluated): the <see cref="BlockXYParameter.CurrentValue"/> falls back to
	/// the stored offset (the second point minus the first point) rather than an empty
	/// placeholder.
	/// </summary>
	[Fact]
	public void XYParameterCurrentValueFallsBackToStoredOffsetTest()
	{
		BlockXYParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(1, 2, 0),
			SecondPoint = new XYZ(3, 4, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to the stored offset (2, 2).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored offset.");
		XY v = param.CurrentValue.Value;
		assertClose(2.0, v.X);
		assertClose(2.0, v.Y);

		// After evaluation (no activated grips), the CurrentValue is the same stored offset.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		v = param.CurrentValue.Value;
		assertClose(2.0, v.X);
		assertClose(2.0, v.Y);
	}

	/// <summary>
	/// Linear parameter (not evaluated): the <see cref="BlockLinearParameter.CurrentValue"/> falls
	/// back to the stored distance (the length of the second point minus the first point) rather
	/// than an empty placeholder.
	/// </summary>
	[Fact]
	public void LinearParameterCurrentValueFallsBackToStoredDistanceTest()
	{
		BlockLinearParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(0, 0, 0),
			SecondPoint = new XYZ(3, 4, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to the stored distance (5).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored distance.");
		assertClose(5.0, param.CurrentValue.Value);

		// After evaluation (no activated grips), the CurrentValue is the same stored distance.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(5.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// Rotation parameter (not evaluated): the <see cref="BlockRotationParameter.CurrentValue"/>
	/// falls back to the stored angle (from the first point to the second point) rather than an
	/// empty placeholder.
	/// </summary>
	[Fact]
	public void RotationParameterCurrentValueFallsBackToStoredAngleTest()
	{
		BlockRotationParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(0, 0, 0),
			SecondPoint = new XYZ(1, 1, 0),
		};

		double expected = Math.Atan2(1.0, 1.0);

		// Do NOT evaluate: the CurrentValue should fall back to the stored angle.
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored angle.");
		assertClose(expected, param.CurrentValue.Value);

		// After evaluation (no activated grips), the CurrentValue is the same stored angle.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(expected, param.CurrentValue.Value);
	}

	/// <summary>
	/// Alignment parameter (not evaluated): the <see cref="BlockAlignmentParameter.CurrentValue"/>
	/// falls back to the stored angle (from the first point to the second point) rather than an
	/// empty placeholder.
	/// </summary>
	[Fact]
	public void AlignmentParameterCurrentValueFallsBackToStoredAngleTest()
	{
		BlockAlignmentParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(0, 0, 0),
			SecondPoint = new XYZ(0, 1, 0),
		};

		double expected = Math.Atan2(1.0, 0.0);

		// Do NOT evaluate: the CurrentValue should fall back to the stored angle.
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored angle.");
		assertClose(expected, param.CurrentValue.Value);

		// After evaluation (no activated grips), the CurrentValue is the same stored angle.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(expected, param.CurrentValue.Value);
	}

	/// <summary>
	/// Polar parameter (not evaluated): the <see cref="BlockPolarParameter.CurrentValue"/> falls
	/// back to the stored (distance, angle) pair rather than an empty placeholder.
	/// </summary>
	[Fact]
	public void PolarParameterCurrentValueFallsBackToStoredPolarTest()
	{
		BlockPolarParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(0, 0, 0),
			SecondPoint = new XYZ(3, 4, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to (5, Atan2(4, 3), 0).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored (distance, angle).");
		XYZ v = param.CurrentValue.Value;
		assertClose(5.0, v.X);
		assertClose(Math.Atan2(4.0, 3.0), v.Y);
		assertClose(0.0, v.Z);

		// After evaluation (no activated grips), the CurrentValue is the same (distance, angle).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		v = param.CurrentValue.Value;
		assertClose(5.0, v.X);
		assertClose(Math.Atan2(4.0, 3.0), v.Y);
		assertClose(0.0, v.Z);
	}

	/// <summary>
	/// Horizontal constraint parameter (not evaluated): the
	/// <see cref="BlockHorizontalConstraintParameter.CurrentValue"/> falls back to the stored
	/// horizontal distance rather than an empty placeholder.
	/// </summary>
	[Fact]
	public void HorizontalConstraintParameterCurrentValueFallsBackToStoredDistanceTest()
	{
		BlockHorizontalConstraintParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(1, 2, 0),
			SecondPoint = new XYZ(4, 9, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to the stored horizontal distance (3).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored distance.");
		assertClose(3.0, param.CurrentValue.Value);

		// After evaluation (no activated grips), the CurrentValue is the same stored distance.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(3.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// Vertical constraint parameter (not evaluated): the
	/// <see cref="BlockVerticalConstraintParameter.CurrentValue"/> falls back to the stored
	/// vertical distance rather than an empty placeholder.
	/// </summary>
	[Fact]
	public void VerticalConstraintParameterCurrentValueFallsBackToStoredDistanceTest()
	{
		BlockVerticalConstraintParameter param = new()
		{
			Id = 1,
			FirstPoint = new XYZ(1, 2, 0),
			SecondPoint = new XYZ(9, 5, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to the stored vertical distance (3).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored distance.");
		assertClose(3.0, param.CurrentValue.Value);

		// After evaluation (no activated grips), the CurrentValue is the same stored distance.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(3.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// Point parameter (not evaluated, not activated): the
	/// <see cref="BlockPointParameter.CurrentValue"/> falls back to the zero displacement rather
	/// than an empty placeholder.
	/// </summary>
	[Fact]
	public void PointParameterCurrentValueFallsBackToZeroDisplacementTest()
	{
		BlockPointParameter param = new()
		{
			Id = 1,
			Location = new XYZ(1, 2, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to the zero displacement.
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the zero displacement.");
		XYZ v = param.CurrentValue.Value;
		assertClose(0.0, v.X);
		assertClose(0.0, v.Y);
		assertClose(0.0, v.Z);

		// After evaluation (no activated grips), the CurrentValue is the zero displacement.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		v = param.CurrentValue.Value;
		assertClose(0.0, v.X);
		assertClose(0.0, v.Y);
		assertClose(0.0, v.Z);
	}

	/// <summary>
	/// Lookup parameter (standalone, no bound lookup table): the value is table-driven and
	/// type-variable (a string or a scalar — never a point), so with no table to resolve there
	/// is simply no value (the value is <see cref="EvaluationValueType.None"/>).
	/// </summary>
	[Fact]
	public void LookupParameterWithoutBoundTableHasUnsetValueTest()
	{
		BlockLookupParameter param = new()
		{
			Id = 1,
			Location = new XYZ(1, 2, 0),
		};

		// Do NOT evaluate: with no bound table, the value is unset (not a point).
		Assert.True(param.CurrentValue.Type == EvaluationValueType.None, "A standalone lookup parameter has no value.");

		// Evaluation (with no table to resolve) likewise leaves the value unset.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.Equal(EvaluationValueType.None, param.CurrentValue.Type);
	}

	/// <summary>
	/// Lookup parameter bound to a <b>text</b> column (<c>95</c> = 1): the value is a
	/// <b>string</b> — the matched cell the table wrote to the parameter's port, or the
	/// column's <c>UnmatchedName</c> default when no row matched.
	/// </summary>
	[Fact]
	public void LookupParameterTextColumnValueIsStringTest()
	{
		// A lookup parameter is the output ("lookup property") column of its action's table.
		BlockLookupAction.ColumnData column = new()
		{
			NodeId = 20,
			ValueType = 1,            // string
			Type = 0,
			IsLookupProperty = true,
			ConnectionName = "lookupString",
			UnmatchedName = "Custom",
		};
		column.Rows.Add("Size 5");

		BlockLookupAction action = new() { Id = 24, Columns = new List<BlockLookupAction.ColumnData> { column } };
		BlockLookupParameter param = new() { Id = 20, ActionId = 24, Location = new XYZ(1, 2, 0) };

		EvaluationGraph graph = new EvaluationGraph();
		EvaluationGraph.Node paramNode = graph.CreateNode();
		paramNode.Id = 20;
		paramNode.Expression = param;
		EvaluationGraph.Node actionNode = graph.CreateNode();
		actionNode.Id = 24;
		actionNode.Expression = action;

		// Before evaluation: the value is the column's default (the UnmatchedName) as a string.
		Assert.Equal(EvaluationValueType.String, param.CurrentValue.Type);
		Assert.Equal("Custom", param.CurrentValue.StringValue);

		// Evaluation with no matching input row: the value stays the default (the UnmatchedName).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.Equal(EvaluationValueType.String, param.CurrentValue.Type);
		Assert.Equal("Custom", param.CurrentValue.StringValue);

		// When the table writes a matched cell to the parameter's port, the value is that cell (a string).
		EvaluationContext context = new EvaluationContext();
		context.SetValue(20, "lookupString", "Size 5");
		Assert.True(param.Evaluate(context), "The evaluation failed.");
		Assert.Equal(EvaluationValueType.String, param.CurrentValue.Type);
		Assert.Equal("Size 5", param.CurrentValue.StringValue);
	}

	/// <summary>
	/// Lookup parameter bound to a <b>numeric</b> column (<c>95</c> = 40): the value is a
	/// <b>scalar</b> — the matched cell the table wrote to the parameter's port, or the
	/// column's <c>UnmatchedName</c> default (parsed) when no row matched.
	/// </summary>
	[Fact]
	public void LookupParameterNumericColumnValueIsScalarTest()
	{
		BlockLookupAction.ColumnData column = new()
		{
			NodeId = 20,
			ValueType = 40,           // double
			Type = 2,
			IsLookupProperty = true,
			ConnectionName = "UpdatedDistance",
			UnmatchedName = "-1",
		};
		column.Rows.Add("5");

		BlockLookupAction action = new() { Id = 24, Columns = new List<BlockLookupAction.ColumnData> { column } };
		BlockLookupParameter param = new() { Id = 20, ActionId = 24, Location = new XYZ(1, 2, 0) };

		EvaluationGraph graph = new EvaluationGraph();
		EvaluationGraph.Node paramNode = graph.CreateNode();
		paramNode.Id = 20;
		paramNode.Expression = param;
		EvaluationGraph.Node actionNode = graph.CreateNode();
		actionNode.Id = 24;
		actionNode.Expression = action;

		// Before evaluation: the value is the column's default (the UnmatchedName) parsed as a scalar.
		Assert.Equal(EvaluationValueType.Double, param.CurrentValue.Type);
		double? defaultVal = param.CurrentValue.DoubleValue;
		Assert.NotNull(defaultVal);
		assertClose(-1.0, defaultVal.Value);

		// Evaluation with no matching input row: the value stays the default scalar.
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(-1.0, param.CurrentValue.DoubleValue.Value);

		// When the table writes a matched cell, the value is that cell as a scalar.
		EvaluationContext context = new EvaluationContext();
		context.SetValue(20, "UpdatedDistance", 5.0);
		Assert.True(param.Evaluate(context), "The evaluation failed.");
		assertClose(5.0, param.CurrentValue.DoubleValue.Value);
	}

	/// <summary>
	/// Base point parameter (not evaluated): the <see cref="BlockBasePointParameter.CurrentValue"/>
	/// falls back to the stored (static) location rather than an empty placeholder.
	/// </summary>
	[Fact]
	public void BasePointParameterCurrentValueFallsBackToStoredLocationTest()
	{
		BlockBasePointParameter param = new()
		{
			Id = 1,
			Location = new XYZ(7, 8, 0),
		};

		// Do NOT evaluate: the CurrentValue should fall back to the stored location (7, 8, 0).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the stored location.");
		XYZ v = param.CurrentValue.Value;
		assertClose(7.0, v.X);
		assertClose(8.0, v.Y);
		assertClose(0.0, v.Z);

		// After evaluation, the CurrentValue is the same stored location (the point never moves).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		v = param.CurrentValue.Value;
		assertClose(7.0, v.X);
		assertClose(8.0, v.Y);
		assertClose(0.0, v.Z);
	}

	/// <summary>
	/// Flip parameter (not evaluated): the <see cref="BlockFlipParameter.CurrentValue"/> falls back
	/// to the initial flip state (0 = base state) rather than an empty placeholder.
	/// </summary>
	[Fact]
	public void FlipParameterCurrentValueFallsBackToInitialStateTest()
	{
		BlockFlipParameter param = new()
		{
			Id = 1,
		};

		// Do NOT evaluate: the CurrentValue should fall back to the initial flip state (0).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the initial flip state.");
		assertClose(0.0, param.CurrentValue.Value);

		// After evaluation, the CurrentValue is the same initial flip state (0).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		assertClose(0.0, param.CurrentValue.Value);
	}

	/// <summary>
	/// Visibility parameter (not evaluated): the <see cref="BlockVisibilityParameter.CurrentValue"/>
	/// falls back to the initial state index (0) rather than an empty placeholder.
	/// </summary>
	[Fact]
	public void VisibilityParameterCurrentValueFallsBackToInitialStateTest()
	{
		BlockVisibilityParameter param = new()
		{
			Id = 1,
		};

		// Do NOT evaluate: the CurrentValue should fall back to the initial state index (0).
		Assert.True(param.CurrentValue.IsSet, "The value should fall back to the initial state index.");
		Assert.Equal(0, param.CurrentValue.Value);

		// After evaluation, the CurrentValue is the same initial state index (0).
		Assert.True(param.Evaluate(new EvaluationContext()), "The evaluation failed.");
		Assert.Equal(0, param.CurrentValue.Value);
	}
}
