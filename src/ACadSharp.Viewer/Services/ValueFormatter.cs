using ACadSharp.Objects.Evaluations;
using CSMath;
using System;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// Human-oriented formatting of evaluation values: the value type (the
/// <see cref="EvaluationValueType"/> enum value) followed by the value itself —
/// angles in degrees (the stored values are radians), polar values as
/// "distance @ angle°" (AutoCAD's polar input notation), and XY values as
/// "x, y".
/// </summary>
public static class ValueFormatter
{
    /// <summary>
    /// Formats an expression's current value for display: the value type
    /// (the <see cref="EvaluationValueType"/> enum value) followed by the value.
    /// Returns "&lt;unset&gt;" for values that have not been evaluated yet.
    /// </summary>
    public static string Format(EvaluationExpression expression)
    {
        EvaluationValue value = expression.CurrentValue;
        if (value.Type == EvaluationValueType.None)
        {
            return "<unset>";
        }

        return $"{value.Type}: {FormatValue(expression, value)}";
    }

    /// <summary>
    /// The value type (the <see cref="EvaluationValueType"/> enum value) of the
    /// expression's current value, or "&lt;unset&gt;" when it has not been evaluated.
    /// </summary>
    public static string TypeText(EvaluationExpression expression)
    {
        EvaluationValue value = expression.CurrentValue;
        return value.Type == EvaluationValueType.None ? "<unset>" : value.Type.ToString();
    }

    /// <summary>
    /// Formats the value itself (without the type prefix). Returns "&lt;unset&gt;" for
    /// values that have not been evaluated yet.
    /// </summary>
    public static string FormatValueOnly(EvaluationExpression expression)
    {
        EvaluationValue value = expression.CurrentValue;
        if (value.Type == EvaluationValueType.None)
        {
            return "<unset>";
        }

        return FormatValue(expression, value);
    }

    /// <summary>
    /// Formats a bare <see cref="EvaluationValue"/> for display (without any type prefix and
    /// without the expression's type-specific interpretation): scalars as "0.##", points as
    /// "(x, y, z)" / "(x, y)", strings quoted, the rest via <see cref="EvaluationValue.ToString"/>.
    /// Used for the per-port / per-edge values in the provenance view, where the value is read
    /// from the evaluation context (there is no owning expression to interpret it with). Returns
    /// the empty string for an unset value.
    /// </summary>
    public static string Format(EvaluationValue value)
    {
        if (value.Type == EvaluationValueType.None)
        {
            return string.Empty;
        }

        return value.Type switch
        {
            EvaluationValueType.Double => (value.DoubleValue ?? 0.0).ToString("0.##"),
            EvaluationValueType.Int => (value.IntValue ?? 0).ToString(),
            EvaluationValueType.String => value.StringValue is { } s ? $"\"{s}\"" : string.Empty,
            EvaluationValueType.Point => value.PointValue is { } p ? $"({p.X:0.##}, {p.Y:0.##}, {p.Z:0.##})" : string.Empty,
            EvaluationValueType.Point2d => value.Point2dValue is { } p2 ? $"({p2.X:0.##}, {p2.Y:0.##})" : string.Empty,
            EvaluationValueType.Char => value.CharValue is { } c ? c.ToString() : string.Empty,
            EvaluationValueType.ObjectId => (value.ObjectIdValue ?? 0).ToString(),
            _ => value.ToString(),
        };
    }

    /// <summary>
    /// Formats the value itself (without the type prefix).
    /// </summary>
    private static string FormatValue(EvaluationExpression expression, EvaluationValue value)
    {
        switch (expression)
        {
            case BlockPolarParameter:
            {
                // The value is a polar-space point: X = distance, Y = angle (radians).
                XYZ p = value.PointValue ?? XYZ.Zero;
                return $"{p.X:0.##} @ {ToDegrees(p.Y):0.##}°";
            }
            case BlockRotationParameter or BlockAlignmentParameter:
                return $"{ToDegrees(value.DoubleValue ?? 0):0.##}°";
            case BlockXYParameter:
            {
                // A Cartesian (x, y) pair; the value is a 2D point.
                XY p2 = value.Point2dValue ?? XY.Zero;
                return $"{p2.X:0.##}, {p2.Y:0.##}";
            }
            default:
                return value.ToString();
        }
    }

    /// <summary>
    /// Converts radians to degrees.
    /// </summary>
    public static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
