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
	/// evaluation; <c>kNone</c> before the first evaluation).
	/// </para>
	/// </summary>
	public double? CurrentValue { get; internal set; }

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