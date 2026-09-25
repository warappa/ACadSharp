using ACadSharp.Attributes;
using CSMath;

namespace ACadSharp.Objects.Evaluations;

[DxfSubClass(DxfSubclassMarker.BlockGrip)]
public abstract class BlockGrip : BlockElement
{
	/// <summary>
	/// Gets or sets the location of the grip.
	/// </summary>
	[DxfCodeValue(1010, 1020, 1030)]
	public XYZ Location { get; set; }

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockGrip;

	/// <summary>
	/// Gets or sets a value indicating whether the grip is cycling.
	/// </summary>
	[DxfCodeValue(280)]
	public bool Cycling { get; set; } = true;

	/// <summary>
	/// Gets or sets the cycling weight of the grip.
	/// </summary>
	[DxfCodeValue(91)]
	public int ExpressionId1 { get; set; }

	/// <summary>
	/// Gets or sets the cycling weight of the grip.
	/// </summary>
	[DxfCodeValue(92)]
	public int ExpressionId2 { get; set; }

	[DxfCodeValue(93)]
	public int Value93 { get; set; }

	/// <summary>
	/// The grip's current position after a user interaction (the activated position).
	/// <para>
	/// When the grip is not activated, its position is <see cref="Location"/> (the base
	/// position) and its displacement is zero.
	/// </para>
	/// </summary>
	public XYZ? ActivatedLocation { get; internal set; }

	/// <summary>
	/// The grip's displacement from its base position (zero when not activated).
	/// </summary>
	public XYZ Displacement => (this.ActivatedLocation ?? this.Location) - this.Location;

	/// <summary>
	/// The current value, correctly typed (hides the base <see cref="EvaluationExpression.CurrentValue"/>;
	/// a computed read of it). For a grip this is the full (X, Y) displacement.
	/// </summary>
	public new EvaluationValue<XYZ> CurrentValue => base.CurrentValue.As<XYZ>();

	/// <summary>
	/// Evaluates the grip: writes its displacement (the "DisplacementX/Y" output ports) into
	/// the context. The displacement is zero when the grip is not activated.
	/// </summary>
	public override bool Evaluate(EvaluationContext context)
	{
		XYZ displacement = this.Displacement;

		context.SetValue(this.Id, "DisplacementX", displacement.X);
		context.SetValue(this.Id, "DisplacementY", displacement.Y);
		base.CurrentValue = EvaluationValue.FromPoint(displacement);

		return true;
	}
}
