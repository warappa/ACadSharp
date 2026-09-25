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
                // A Cartesian (x, y) pair; drop the always-zero Z.
                XYZ p = value.PointValue ?? XYZ.Zero;
                return $"{p.X:0.##}, {p.Y:0.##}";
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
