using CSMath;
using System;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// The shape of an <see cref="EvaluationValue"/>.
/// <para>
/// Mirrors the ObjectARX <c>AcDbEvalVariant::Type</c> enumeration — <c>kNone</c> (uninitialized),
/// <c>kDouble</c>, <c>kPoint2d</c>, <c>kPoint3d</c>, and <c>kString</c> — plus the integer, character,
/// and object-id forms that the <c>AcDbEvalVariant</c> constructors accept (<c>Adesk::Int32</c>,
/// <c>short</c>, <c>ACAD_CHAR</c>, and <c>AcDbObjectId</c>).
/// </para>
/// <para>
/// The real <c>AcDbEvalVariant</c> is a lightweight <c>resbuf</c> wrapper, so it can hold a
/// <em>string</em> (it "manages the copying of strings by calling <c>acutNewString()</c>"; the
/// <c>AcDbEvalVariant(const ACHAR*)</c> constructor sets the type to <c>kString</c>). Dynamic blocks
/// and lookup tables make use of this: a lookup table's text columns (for example the state names
/// "Custom", "look up", "look straight") are stored as text cells and compared as strings.
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

	/// <summary>A 2D point. Mirrors <c>AcDbEvalVariant::kPoint2d</c>.</summary>
	Point2d,

	/// <summary>A string. Mirrors <c>AcDbEvalVariant::kString</c>.</summary>
	String,

	/// <summary>An integer. Mirrors the <c>AcDbEvalVariant(Adesk::Int32)</c> / <c>(short)</c> constructors.</summary>
	Int,

	/// <summary>A single character. Mirrors the <c>AcDbEvalVariant(ACAD_CHAR)</c> constructor.</summary>
	Char,

	/// <summary>An object reference (a handle). Mirrors the <c>AcDbEvalVariant(AcDbObjectId)</c> constructor.</summary>
	ObjectId,
}

/// <summary>
/// A shape-agnostic holder for an evaluation value — the "object" form of a node's value.
/// <para>
/// Mirrors the ObjectARX <c>AcDbEvalVariant</c> (a lightweight resbuf wrapper). It holds exactly one
/// value of one of the shapes in <see cref="EvaluationValueType"/> — a scalar (<see cref="double"/>),
/// a 3D point (<see cref="XYZ"/>), a 2D point (<see cref="XY"/>), a string (<see cref="string"/>),
/// an integer (<see cref="int"/>), a character (<see cref="char"/>), or an object handle
/// (<see cref="long"/>). A multi-valued expression (for example a point or XY parameter) can expose
/// its <em>whole</em> value rather than a single representative component.
/// </para>
/// <para>
/// The held value is stored as a single <c>object</c> payload tagged by <see cref="Type"/> (a
/// discriminated union), so the shape set can be extended without adding a field per type. Use the
/// safe accessors (<see cref="DoubleValue"/>, <see cref="StringValue"/>, …) for a null-on-mismatch read,
/// or <see cref="As{T}"/> for a strict typed <see cref="EvaluationValue{T}"/> view.
/// </para>
/// </summary>
public sealed class EvaluationValue
{
	/// <summary>
	/// The shape of the held value.
	/// </summary>
	public EvaluationValueType Type { get; }

	/// <summary>
	/// The held value, boxed. Non-null for every shape except <see cref="EvaluationValueType.None"/>.
	/// </summary>
	private readonly object _payload;

	private EvaluationValue(EvaluationValueType type, object payload = null)
	{
		this.Type = type;
		this._payload = payload;
	}

	/// <summary>
	/// The unset value (before the first evaluation). Mirrors <c>AcDbEvalVariant::kNone</c>.
	/// </summary>
	public static EvaluationValue None { get; } = new(EvaluationValueType.None);

	/// <summary>Creates a value holding the given scalar.</summary>
	public static EvaluationValue FromDouble(double value) => new(EvaluationValueType.Double, value);

	/// <summary>Creates a value holding the given 3D point.</summary>
	public static EvaluationValue FromPoint(XYZ point) => new(EvaluationValueType.Point, point);

	/// <summary>Creates a value holding the given 2D point.</summary>
	public static EvaluationValue FromPoint2d(XY point) => new(EvaluationValueType.Point2d, point);

	/// <summary>Creates a value holding the given string.</summary>
	public static EvaluationValue FromString(string value) => new(EvaluationValueType.String, value);

	/// <summary>Creates a value holding the given integer.</summary>
	public static EvaluationValue FromInt(int value) => new(EvaluationValueType.Int, value);

	/// <summary>Creates a value holding the given character.</summary>
	public static EvaluationValue FromChar(char value) => new(EvaluationValueType.Char, value);

	/// <summary>Creates a value holding the given object handle.</summary>
	public static EvaluationValue FromObjectId(long handle) => new(EvaluationValueType.ObjectId, handle);

	// ------------------------------------------------------------------
	// Safe accessors: return null when the shape does not match.
	// ------------------------------------------------------------------

	/// <summary>
	/// The scalar value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.Double"/>.
	/// </summary>
	public double? DoubleValue => this._payload is double v ? v : null;

	/// <summary>
	/// The 3D point value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.Point"/>.
	/// </summary>
	public XYZ? PointValue => this._payload is XYZ v ? v : null;

	/// <summary>
	/// The 2D point value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.Point2d"/>.
	/// </summary>
	public XY? Point2dValue => this._payload is XY v ? v : null;

	/// <summary>
	/// The string value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.String"/>.
	/// </summary>
	public string StringValue => this._payload is string v ? v : null;

	/// <summary>
	/// The integer value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.Int"/>.
	/// </summary>
	public int? IntValue => this._payload is int v ? v : null;

	/// <summary>
	/// The character value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.Char"/>.
	/// </summary>
	public char? CharValue => this._payload is char v ? v : null;

	/// <summary>
	/// The object handle value; non-null only when <see cref="Type"/> is <see cref="EvaluationValueType.ObjectId"/>.
	/// </summary>
	public long? ObjectIdValue => this._payload is long v ? v : null;

	/// <summary>
	/// Interprets the value as <typeparam name="T"/>.
	/// <para>
	/// Returns <see cref="EvaluationValue{T}.None"/> when this value is unset (<see cref="EvaluationValueType.None"/>);
	/// returns the typed value when the stored shape matches <typeparam name="T"/>; otherwise throws
	/// <see cref="InvalidOperationException"/>.
	/// </para>
	/// </summary>
	/// <typeparam name="T">The target type (for example <see cref="double"/>, <see cref="XYZ"/>, or <see cref="string"/>).</typeparam>
	/// <returns>The typed value, or <see cref="EvaluationValue{T}.None"/> when unset.</returns>
	/// <exception cref="InvalidOperationException">The stored shape does not match <typeparam name="T"/>.</exception>
	public EvaluationValue<T> As<T>()
	{
		if (this.Type == EvaluationValueType.None)
		{
			return EvaluationValue<T>.None;
		}

		if (this._payload is T value)
		{
			return EvaluationValue<T>.Of(value);
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
				return this._payload is double d ? d.ToString("0.###") : "null";
			case EvaluationValueType.Point:
				return this._payload is XYZ p ? $"({p.X:F2},{p.Y:F2},{p.Z:F2})" : "null";
			case EvaluationValueType.Point2d:
				return this._payload is XY p2 ? $"({p2.X:F2},{p2.Y:F2})" : "null";
			case EvaluationValueType.String:
				return this._payload is string s ? s : "null";
			case EvaluationValueType.Int:
				return this._payload is int i ? i.ToString() : "null";
			case EvaluationValueType.Char:
				return this._payload is char c ? c.ToString() : "null";
			case EvaluationValueType.ObjectId:
				return this._payload is long h ? h.ToString() : "null";
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
/// <typeparam name="T">The value type (for example <see cref="double"/>, <see cref="XYZ"/>, or <see cref="string"/>).</typeparam>
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
	/// <c>default</c> (for example a <c>0.0</c> displacement or an empty string) still reads as set.
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
