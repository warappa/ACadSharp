using ACadSharp.Objects.Evaluations;
using CSMath;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ACadSharp.Tests.Objects;

/// <summary>
/// Unit tests for the shape-agnostic evaluation value model (<see cref="EvaluationValue"/>),
/// the <see cref="EvaluationContext"/>, and the type-aware <see cref="BlockLookupAction"/>.
/// </summary>
public class EvaluationValueTests
{
	// ------------------------------------------------------------------
	// EvaluationValue: round-trips for every supported shape.
	// ------------------------------------------------------------------

	[Fact]
	public void NoneValueTest()
	{
		EvaluationValue value = EvaluationValue.None;

		Assert.Equal(EvaluationValueType.None, value.Type);
		Assert.Null(value.DoubleValue);
		Assert.Null(value.PointValue);
		Assert.Null(value.Point2dValue);
		Assert.Null(value.StringValue);
		Assert.Null(value.IntValue);
		Assert.Null(value.CharValue);
		Assert.Null(value.ObjectIdValue);

		// As<T>() on an unset value returns the unset typed view (never throws).
		EvaluationValue<double> asDouble = value.As<double>();
		Assert.False(asDouble.IsSet);
		Assert.Equal("<unset>", value.ToString());
	}

	[Fact]
	public void DoubleRoundTripTest()
	{
		EvaluationValue value = EvaluationValue.FromDouble(2.5);

		Assert.Equal(EvaluationValueType.Double, value.Type);
		Assert.Equal(2.5, value.DoubleValue);
		Assert.Null(value.StringValue);

		EvaluationValue<double> asDouble = value.As<double>();
		Assert.True(asDouble.IsSet);
		Assert.Equal(2.5, asDouble.Value);
		Assert.Equal("2.5", value.ToString());
	}

	[Fact]
	public void PointRoundTripTest()
	{
		XYZ point = new(1, 2, 3);
		EvaluationValue value = EvaluationValue.FromPoint(point);

		Assert.Equal(EvaluationValueType.Point, value.Type);
		Assert.Equal(point, value.PointValue);
		Assert.Null(value.Point2dValue);
		Assert.Null(value.DoubleValue);

		EvaluationValue<XYZ> asPoint = value.As<XYZ>();
		Assert.True(asPoint.IsSet);
		Assert.Equal(point, asPoint.Value);
		Assert.Equal("(1.00,2.00,3.00)", value.ToString());
	}

	[Fact]
	public void Point2dRoundTripTest()
	{
		XY point = new(1.5, -2.25);
		EvaluationValue value = EvaluationValue.FromPoint2d(point);

		Assert.Equal(EvaluationValueType.Point2d, value.Type);
		Assert.Equal(point, value.Point2dValue);
		Assert.Null(value.PointValue);

		EvaluationValue<XY> asPoint = value.As<XY>();
		Assert.True(asPoint.IsSet);
		Assert.Equal(point, asPoint.Value);
		Assert.Equal("(1.50,-2.25)", value.ToString());
	}

	[Fact]
	public void StringRoundTripTest()
	{
		EvaluationValue value = EvaluationValue.FromString("Size 5");

		Assert.Equal(EvaluationValueType.String, value.Type);
		Assert.Equal("Size 5", value.StringValue);
		Assert.Null(value.DoubleValue);

		EvaluationValue<string> asString = value.As<string>();
		Assert.True(asString.IsSet);
		Assert.Equal("Size 5", asString.Value);
		// A string's ToString() is the raw string (the viewer displays it verbatim).
		Assert.Equal("Size 5", value.ToString());
	}

	[Fact]
	public void IntRoundTripTest()
	{
		EvaluationValue value = EvaluationValue.FromInt(42);

		Assert.Equal(EvaluationValueType.Int, value.Type);
		Assert.Equal(42, value.IntValue);
		Assert.Null(value.DoubleValue);

		EvaluationValue<int> asInt = value.As<int>();
		Assert.True(asInt.IsSet);
		Assert.Equal(42, asInt.Value);
		Assert.Equal("42", value.ToString());
	}

	[Fact]
	public void CharRoundTripTest()
	{
		EvaluationValue value = EvaluationValue.FromChar('A');

		Assert.Equal(EvaluationValueType.Char, value.Type);
		Assert.Equal('A', value.CharValue);
		Assert.Null(value.IntValue);

		EvaluationValue<char> asChar = value.As<char>();
		Assert.True(asChar.IsSet);
		Assert.Equal('A', asChar.Value);
		Assert.Equal("A", value.ToString());
	}

	[Fact]
	public void ObjectIdRoundTripTest()
	{
		EvaluationValue value = EvaluationValue.FromObjectId(1234567);

		Assert.Equal(EvaluationValueType.ObjectId, value.Type);
		Assert.Equal(1234567L, value.ObjectIdValue);

		EvaluationValue<long> asId = value.As<long>();
		Assert.True(asId.IsSet);
		Assert.Equal(1234567L, asId.Value);
		Assert.Equal("1234567", value.ToString());
	}

	[Fact]
	public void AsMismatchThrowsTest()
	{
		// A double value cannot be interpreted as a string (and vice versa): the shapes
		// are distinct, so a mismatch throws.
		Assert.Throws<InvalidOperationException>(() => EvaluationValue.FromDouble(1.0).As<string>());
		Assert.Throws<InvalidOperationException>(() => EvaluationValue.FromString("x").As<double>());
		Assert.Throws<InvalidOperationException>(() => EvaluationValue.FromPoint(new XYZ(1, 2, 3)).As<XY>());
		Assert.Throws<InvalidOperationException>(() => EvaluationValue.FromInt(1).As<double>());
	}

	[Fact]
	public void TypedViewTest()
	{
		// A legitimate default (0.0) still reads as set (the dedicated IsSet flag).
		EvaluationValue<double> zero = EvaluationValue<double>.Of(0.0);
		Assert.True(zero.IsSet);
		Assert.Equal(0.0, zero.Value);

		EvaluationValue<double> none = EvaluationValue<double>.None;
		Assert.False(none.IsSet);
		Assert.Equal("<unset>", none.ToString());

		// An empty string is a set value.
		EvaluationValue<string> empty = EvaluationValue<string>.Of(string.Empty);
		Assert.True(empty.IsSet);
		Assert.Equal(string.Empty, empty.Value);
	}

	// ------------------------------------------------------------------
	// EvaluationContext: mixed shapes in the same context.
	// ------------------------------------------------------------------

	[Fact]
	public void ContextMixedShapesTest()
	{
		EvaluationContext context = new();

		context.SetValue(1, "distance", 5.0);
		context.SetValue(2, "name", "Size 5");
		context.SetValue(3, "point", EvaluationValue.FromPoint(new XYZ(1, 2, 3)));

		// Scalar read.
		Assert.True(context.TryGetValue(1, "distance", out double d));
		Assert.Equal(5.0, d);

		// String read.
		Assert.True(context.TryGetValue(2, "name", out string s));
		Assert.Equal("Size 5", s);

		// Full-value read.
		Assert.True(context.TryGetValue(3, "point", out EvaluationValue v));
		Assert.Equal(EvaluationValueType.Point, v.Type);
		Assert.Equal(new XYZ(1, 2, 3), v.PointValue);

		// A scalar read of a string port fails (the shape does not match).
		Assert.False(context.TryGetValue(2, "name", out double _));
		// A string read of a scalar port fails.
		Assert.False(context.TryGetValue(1, "distance", out string _));
	}

	[Fact]
	public void ContextHasValueAndClearTest()
	{
		EvaluationContext context = new();

		Assert.False(context.HasValue(1, "x"));
		context.SetValue(1, "x", "hello");
		Assert.True(context.HasValue(1, "x"));

		context.Clear();
		Assert.False(context.HasValue(1, "x"));
		Assert.False(context.TryGetValue(1, "x", out double _));
	}

	// ------------------------------------------------------------------
	// BlockLookupAction: type-aware row matching.
	// ------------------------------------------------------------------

	/// <summary>
	/// Builds a lookup table mirroring the real sample (BLOCKLOOKUPPARAMETER.dxf):
	/// 3 rows x 5 columns — numeric columns (UpdatedDistance, UpdatedX, UpdatedY) and
	/// text columns (both connected to the "lookupString" port of nodes 20 and 36).
	/// </summary>
	/// <remarks>
	/// Row 0: 5, 2.5, 2.5, "Size 5", "Alt size 5"
	/// Row 1: 6, 3, 3, "Size 6 " (trailing space, as in the file), "Alt size 6"
	/// Row 2: 8, 4, 4, "Size 8", "Alt size 8"
	/// </remarks>
	private static BlockLookupAction buildSampleTable()
	{
		BlockLookupAction.ColumnData distance = new() { NodeId = 11, ValueType = 40, Type = 2, ConnectionName = "UpdatedDistance" };
		BlockLookupAction.ColumnData updatedX = new() { NodeId = 29, ValueType = 40, Type = 2, ConnectionName = "UpdatedX" };
		BlockLookupAction.ColumnData updatedY = new() { NodeId = 29, ValueType = 40, Type = 2, ConnectionName = "UpdatedY" };
		BlockLookupAction.ColumnData textA = new() { NodeId = 20, ValueType = 1, Type = 0, ConnectionName = "lookupString" };
		BlockLookupAction.ColumnData textB = new() { NodeId = 36, ValueType = 1, Type = 0, ConnectionName = "lookupString" };

		// Rows (mirroring the real file, row 1's text cell carries a trailing space).
		distance.Rows.Add("5"); distance.Rows.Add("6"); distance.Rows.Add("8");
		updatedX.Rows.Add("2.5"); updatedX.Rows.Add("3"); updatedX.Rows.Add("4");
		updatedY.Rows.Add("2.5"); updatedY.Rows.Add("3"); updatedY.Rows.Add("4");
		textA.Rows.Add("Size 5"); textA.Rows.Add("Size 6 "); textA.Rows.Add("Size 8");
		textB.Rows.Add("Alt size 5"); textB.Rows.Add("Alt size 6"); textB.Rows.Add("Alt size 8");

		BlockLookupAction action = new();
		action.Columns = new List<BlockLookupAction.ColumnData> { distance, updatedX, updatedY, textA, textB };
		return action;
	}

	/// <summary>
	/// Fills the context with the inputs of the given row of the sample table.
	/// </summary>
	private static EvaluationContext fillRow(int row, bool withText = true)
	{
		double[] distance = { 5, 6, 8 };
		double[] xy = { 2.5, 3, 4 };
		string[] textA = { "Size 5", "Size 6 ", "Size 8" };
		string[] textB = { "Alt size 5", "Alt size 6", "Alt size 8" };

		EvaluationContext context = new();
		context.SetValue(11, "UpdatedDistance", distance[row]);
		context.SetValue(29, "UpdatedX", xy[row]);
		context.SetValue(29, "UpdatedY", xy[row]);
		if (withText)
		{
			context.SetValue(20, "lookupString", textA[row]);
			context.SetValue(36, "lookupString", textB[row]);
		}
		return context;
	}

	[Fact]
	public void LookupNumericAndTextMatchTest()
	{
		BlockLookupAction action = buildSampleTable();

		// All five inputs match row 0 (including both text columns).
		Assert.True(action.Evaluate(fillRow(0)));
		assertRow(0, action);

		// Row 1's text cell "Size 6 " carries a trailing space (as in the real file):
		// the exact (case-insensitive) comparison matches it.
		Assert.True(action.Evaluate(fillRow(1)));
		assertRow(1, action);

		// Row 2.
		Assert.True(action.Evaluate(fillRow(2)));
		assertRow(2, action);
	}

	[Fact]
	public void LookupTextCaseInsensitiveTest()
	{
		BlockLookupAction action = buildSampleTable();
		EvaluationContext context = fillRow(2);

		// Case-insensitive text match: "size 8" matches the cell "Size 8".
		context.SetValue(20, "lookupString", "size 8");
		Assert.True(action.Evaluate(context));
		assertRow(2, action);
	}

	[Fact]
	public void LookupTextNoMatchTest()
	{
		BlockLookupAction action = buildSampleTable();
		EvaluationContext context = fillRow(0);

		// A text input that matches no cell: no row matches.
		context.SetValue(20, "lookupString", "Unknown");
		Assert.True(action.Evaluate(context));
		assertRow(-1, action);
	}

	[Fact]
	public void LookupMissingTextPortTest()
	{
		BlockLookupAction action = buildSampleTable();

		// The text ports are missing (never set): a null input never matches a text cell.
		Assert.True(action.Evaluate(fillRow(0, withText: false)));
		assertRow(-1, action);
	}

	[Fact]
	public void LookupWrongShapeTest()
	{
		BlockLookupAction action = buildSampleTable();
		EvaluationContext context = fillRow(0);

		// A string stored on a numeric column's port: the scalar read fails (0), so no
		// row matches — the shape mismatch is not silently coerced.
		context.SetValue(11, "UpdatedDistance", "5");
		Assert.True(action.Evaluate(context));
		assertRow(-1, action);
	}

	[Fact]
	public void LookupNoColumnsTest()
	{
		BlockLookupAction action = new();

		// No columns: the matched row is -1.
		Assert.True(action.Evaluate(new EvaluationContext()));
		assertRow(-1, action);
	}

	[Fact]
	public void ColumnIsTextTest()
	{
		// A text column has ValueType = 1; a numeric column has ValueType = 40.
		BlockLookupAction action = buildSampleTable();

		Assert.False(action.Columns[0].IsText);
		Assert.False(action.Columns[1].IsText);
		Assert.False(action.Columns[2].IsText);
		Assert.True(action.Columns[3].IsText);
		Assert.True(action.Columns[4].IsText);
	}

	private static void assertRow(int expected, BlockLookupAction action)
	{
		// The test table has no output column (IsLookupProperty = false), so the result is
		// the matched row index as a scalar.
		EvaluationValue value = action.CurrentValue;
		Assert.Equal(EvaluationValueType.Double, value.Type);
		Assert.True(value.DoubleValue.HasValue, "The lookup action's value is not a scalar.");
		Assert.True(Math.Abs(value.DoubleValue.Value - expected) < 1e-6, $"Expected row {expected}, got {value}.");
	}

	[Fact]
	public void LookupTextOutputResultTest()
	{
		// A table with a text output column (IsLookupProperty = true): the result is the
		// matched cell as a string (a string for a text column — the kString shape).
		BlockLookupAction.ColumnData input = new() { NodeId = 1, ValueType = 40, Type = 2, ConnectionName = "value" };
		BlockLookupAction.ColumnData output = new() { NodeId = 2, ValueType = 1, Type = 0, IsLookupProperty = true, ConnectionName = "lookupString", UnmatchedName = "Custom" };
		input.Rows.Add("5");
		input.Rows.Add("6");
		output.Rows.Add("Size 5");
		output.Rows.Add("Size 6");

		BlockLookupAction action = new();
		action.Columns = new List<BlockLookupAction.ColumnData> { input, output };

		EvaluationContext context = new();
		context.SetValue(1, "value", 5.0);

		// Match: the result is the matched text cell.
		Assert.True(action.Evaluate(context));
		Assert.Equal(EvaluationValueType.String, action.CurrentValue.Type);
		Assert.Equal("Size 5", action.CurrentValue.StringValue);

		// No match: the result is the UnmatchedName (a string).
		context.SetValue(1, "value", 99.0);
		Assert.True(action.Evaluate(context));
		Assert.Equal(EvaluationValueType.String, action.CurrentValue.Type);
		Assert.Equal("Custom", action.CurrentValue.StringValue);
	}

	[Fact]
	public void LookupNumericOutputResultTest()
	{
		// A table with a numeric output column (IsLookupProperty = true): the result is the
		// matched cell as a scalar.
		BlockLookupAction.ColumnData input = new() { NodeId = 1, ValueType = 40, Type = 2, ConnectionName = "value" };
		BlockLookupAction.ColumnData output = new() { NodeId = 2, ValueType = 40, Type = 2, IsLookupProperty = true, ConnectionName = "result" };
		input.Rows.Add("5");
		output.Rows.Add("2.5");

		BlockLookupAction action = new();
		action.Columns = new List<BlockLookupAction.ColumnData> { input, output };

		EvaluationContext context = new();
		context.SetValue(1, "value", 5.0);

		// Match: the result is the matched numeric cell.
		Assert.True(action.Evaluate(context));
		Assert.Equal(EvaluationValueType.Double, action.CurrentValue.Type);
		Assert.True(Math.Abs(action.CurrentValue.DoubleValue.Value - 2.5) < 1e-6, $"Expected 2.5, got {action.CurrentValue}.");

		// No match: the result is -1.
		context.SetValue(1, "value", 99.0);
		Assert.True(action.Evaluate(context));
		Assert.Equal(EvaluationValueType.Double, action.CurrentValue.Type);
		Assert.True(Math.Abs(action.CurrentValue.DoubleValue.Value - (-1)) < 1e-6, $"Expected -1, got {action.CurrentValue}.");
	}
}
