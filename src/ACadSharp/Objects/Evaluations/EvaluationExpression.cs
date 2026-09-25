using ACadSharp.Attributes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents an evaluation expression used in AutoCAD to define dynamic block parameters and constraints.
/// </summary>
[DxfSubClass(DxfSubclassMarker.EvalGraphExpr)]
public abstract class EvaluationExpression : NonGraphicalObject
{
	/// <summary>
	/// Gets or sets the evaluated value of the expression.
	/// </summary>
	public DxfValuePair EvaluatedValue { get; set; }

	/// <summary>
	/// Gets or sets the unique identifier for the evaluation expression.
	/// </summary>
	[DxfCodeValue(90)]
	public int Id { get; set; }

	/// <inheritdoc/>
	public override ObjectType ObjectType => ObjectType.UNLISTED;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.EvalGraphExpr;

	[DxfCodeValue(98)]
	public int Value98 { get; set; }

	[DxfCodeValue(99)]
	public int Value99 { get; set; }

	internal int Unknown { get; set; } = -1;

	/// <summary>
	/// The current value of the expression, updated during <see cref="Evaluate"/>.
	/// <para>
	/// Mirrors the ObjectARX <c>AcDbEvalExpr::value()</c> (the node's value, updated during
	/// evaluation; <c>kNone</c> before the first evaluation). The value is a shape-agnostic
	/// <see cref="EvaluationValue"/> ("object") that can hold any of the shapes in
	/// <see cref="EvaluationValueType"/> — a scalar, a 3D point, a 2D point, a string, an
	/// integer, a character, or an object id — so a multi-valued expression (for example a
	/// point or XY parameter) carries its <em>whole</em> value rather than a single
	/// representative component, and a text value (for example a lookup table's state name)
	/// flows through the graph as a string.
	/// </para>
	/// <para>
	/// A leaf that has a single, well-known value shape exposes a typed view under the same name
	/// (a <c>new</c> <see cref="EvaluationValue{T}"/> property reading <c>base.CurrentValue.As&lt;T&gt;()</c>);
	/// this base property is the single storage that <see cref="Evaluate"/> writes.
	/// </para>
	/// </summary>
	public EvaluationValue CurrentValue { get; internal set; } = EvaluationValue.None;

	/// <summary>
	/// Evaluates the expression, writing its output values into the <paramref name="context"/>.
	/// <para>
	/// Mirrors the ObjectARX <c>AcDbEvalExpr::evaluate()</c>, whose default implementation is a
	/// no-op returning success. Subclasses override this to compute their value from the
	/// values of the nodes they depend on (read from the context) and to write their output
	/// values (keyed by port name) for the nodes that depend on them.
	/// </para>
	/// </summary>
	/// <param name="context">The evaluation context (the value store).</param>
	/// <returns>True when the evaluation succeeded; false when it failed (which aborts the
	/// graph evaluation, matching the ObjectARX behaviour).</returns>
	public virtual bool Evaluate(EvaluationContext context)
	{
		return true;
	}
}