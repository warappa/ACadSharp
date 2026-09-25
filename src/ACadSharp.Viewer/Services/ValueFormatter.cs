using ACadSharp.Objects.Evaluations;
using CSMath;
using System;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// Human-oriented formatting of evaluation values: angles in degrees
/// (the stored values are radians), polar values as "distance @ angle°"
/// (AutoCAD's polar input notation), and XY values as "x, y".
/// </summary>
public static class ValueFormatter
{
    /// <summary>
    /// Formats an expression's current value for display. Returns
    /// "&lt;unset&gt;" for values that have not been evaluated yet.
    /// </summary>
    public static string Format(EvaluationExpression expression)
    {
        EvaluationValue value = expression.CurrentValue;
        if (value.Type == EvaluationValueType.None)
        {
            return "<unset>";
        }

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
