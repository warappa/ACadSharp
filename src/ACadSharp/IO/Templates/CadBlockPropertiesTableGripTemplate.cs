using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal partial class CadBlockPropertiesTableGripTemplate : CadEvaluationExpressionTemplate
{
	public BlockPropertiesTableGrip BlockPropertiesTableGrip { get { return this.CadObject as BlockPropertiesTableGrip; } }

	public CadBlockPropertiesTableGripTemplate() : base(new BlockPropertiesTableGrip())
	{
	}

	public CadBlockPropertiesTableGripTemplate(BlockPropertiesTableGrip grip)
		: base(grip)
	{
	}
}
