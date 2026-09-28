using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal partial class CadBlockPropertiesTableTemplate : CadEvaluationExpressionTemplate
{
	public BlockPropertiesTable BlockPropertiesTable { get { return this.CadObject as BlockPropertiesTable; } }

	public CadBlockPropertiesTableTemplate() : base(new BlockPropertiesTable())
	{
	}

	public CadBlockPropertiesTableTemplate(BlockPropertiesTable table)
		: base(table)
	{
	}
}
