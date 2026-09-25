using CSMath;
using System;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// The shape of an <see cref="EvaluationValue"/>.
/// <para>
/// Mirrors the ObjectARX <c>AcDbEvalVariant::Type</c> enumeration: <c>None</c> (uninitialized),
/// <c>Double</c> (<c>kDouble</c>), and <c>Point</c> (<c>kPoint3d</c>).
/// </para>
/// </summary>
public enum EvaluationValueType
{
	/// <summary>Unset (before the first evaluation). Mirrors <c>AcDbEvalVariant::kNone</c>.</summary>
	None,

	/// <summary>A scalar. Mirrors <c>AcDbEvalVariant::kDouble</c>.</summary>
	Double,

	/// <summary>A 3D point. Mirrors <c>AcDbEvalVariant::kPoint3d</c>.</summary>
	Point,
}

/// <summary>
/// A shape-agnostic holder for an evaluation value — the "object" form of a node's value.
/// <para>
/// Mirrors the ObjectARX <c>AcDbEvalVariant</c> (a lightweight resbuf wrapper). It holds either a
/// scalar (a <see cref="double"/>) or a point (an <see cref="XYZ"/>), so a multi-valued expression
/// (for example a point or XY parameter) can expose its <em>whole</em> value rather than a single
/// representative component.
/// </para>
/// <para>
/// Use <see cref="As{T}"/> to obtain a typed <see cref="EvaluationValue{T}"/> view.
/// </para>
/// </summary>
public sealed class EvaluationValue
{
	/// <summary>
	/// The shape of the held value.
	/// </summary>
	public EvaluationValueType Type { get; }

	/// <summary>
	/// The scalar value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.Double"/>.
	/// </summary>
	public double? DoubleValue { get; }

	/// <summary>
	/// The point value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.Point"/>.
	/// </summary>
	public XYZ? PointValue { get; }

	private EvaluationValue(EvaluationValueType type, double? doubleValue = null, XYZ? pointValue = null)
	{
		this.Type = type;
		this.DoubleValue = doubleValue;
		this.PointValue = pointValue;
	}

	/// <summary>
	/// The unset value (before the first evaluation). Mirrors <c>AcDbEvalVariant::kNone</c>.
	/// </summary>
	public static EvaluationValue None { get; } = new(EvaluationValueType.None);

	/// <summary>
	/// Creates a value holding the given scalar.
	/// </summary>
	public static EvaluationValue FromDouble(double value) => new(EvaluationValueType.Double, doubleValue: value);

	/// <summary>
	/// Creates a value holding the given point.
	/// </summary>
	public static EvaluationValue FromPoint(XYZ point) => new(EvaluationValueType.Point, pointValue: point);

	/// <summary>
	/// Interprets the value as <typeparam name="T"/>.
	/// <para>
	/// Returns <see cref="EvaluationValue{T}.None"/> when this value is unset (<see cref="EvaluationValueType.None"/>);
	/// returns the typed value when the stored shape matches <typeparam name="T"/>; otherwise throws
	/// <see cref="InvalidOperationException"/>.
	/// </para>
	/// </summary>
	/// <typeparam name="T">The target type (<see cref="double"/> or <see cref="XYZ"/>).</typeparam>
	/// <returns>The typed value, or <see cref="EvaluationValue{T}.None"/> when unset.</returns>
	/// <exception cref="InvalidOperationException">The stored shape does not match <typeparam name="T"/>.</exception>
	public EvaluationValue<T> As<T>()
	{
		if (this.Type == EvaluationValueType.None)
		{
			return EvaluationValue<T>.None;
		}

		if (this.Type == EvaluationValueType.Double)
		{
			// T is double: upcast to object, then downcast to the open EvaluationValue<T>.
			if (typeof(T) == typeof(double))
			{
				return (EvaluationValue<T>)(object)EvaluationValue<double>.Of(this.DoubleValue.Value);
			}
			throw new InvalidOperationException($"Cannot interpret a {this.Type} evaluation value as {typeof(T).Name}.");
		}

		if (this.Type == EvaluationValueType.Point)
		{
			// T is XYZ: upcast to object, then downcast to the open EvaluationValue<T>.
			if (typeof(T) == typeof(XYZ))
			{
				return (EvaluationValue<T>)(object)EvaluationValue<XYZ>.Of(this.PointValue.Value);
			}
			throw new InvalidOperationException($"Cannot interpret a {this.Type} evaluation value as {typeof(T).Name}.");
		}

		throw new InvalidOperationException($"Cannot interpret a {this.Type} evaluation value as {typeof(T).Name}.");
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		switch (this.Type)
		{
			case EvaluationValueType.None:
				return "<unset>";
			case EvaluationValueType.Double:
				return this.DoubleValue?.ToString("0.###") ?? "null";
			case EvaluationValueType.Point:
				XYZ? p = this.PointValue;
				return p is null ? "null" : $"({p.Value.X:F2},{p.Value.Y:F2},{p.Value.Z:F2})";
			default:
				return "?";
		}
	}
}

/// <summary>
/// A typed view of an evaluation value: a single value of type <typeparam name="T"/>.
/// <para>
/// Obtain an instance from the shape-agnostic <see cref="EvaluationValue"/> via
/// <see cref="EvaluationValue.As{T}"/>. A value is either <em>set</em> (carrying a value) or
/// <em>unset</em> (<see cref="None"/>).
/// </para>
/// </summary>
/// <typeparam name="T">The value type (for example <see cref="double"/> or <see cref="XYZ"/>).</typeparam>
public sealed class EvaluationValue<T>
{
	private readonly bool _isSet;

	/// <summary>
	/// The held value.
	/// </summary>
	public T Value { get; }

	/// <summary>
	/// Whether a value is held (as opposed to <see cref="None"/>).
	/// <para>
	/// Tracked with a dedicated flag (not derived from the value) so that a legitimate
	/// <c>default</c> (for example a <c>0.0</c> displacement) still reads as set.
	/// </para>
	/// </summary>
	public bool IsSet => this._isSet;

	private EvaluationValue(T value, bool isSet)
	{
		this.Value = value;
		this._isSet = isSet;
	}

	/// <summary>
	/// The unset value (no value held).
	/// </summary>
	public static EvaluationValue<T> None { get; } = new(default, false);

	/// <summary>
	/// Creates a value holding the given value.
	/// </summary>
	public static EvaluationValue<T> Of(T value) => new(value, true);

	/// <inheritdoc/>
	public override string ToString() => this.IsSet ? this.Value?.ToString() ?? "null" : "<unset>";
}
