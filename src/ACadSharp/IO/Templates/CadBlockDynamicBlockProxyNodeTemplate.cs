using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal partial class CadBlockDynamicBlockProxyNodeTemplate : CadEvaluationExpressionTemplate
{
	public BlockDynamicBlockProxyNode BlockDynamicBlockProxyNode { get { return this.CadObject as BlockDynamicBlockProxyNode; } }

	public CadBlockDynamicBlockProxyNodeTemplate() : base(new BlockDynamicBlockProxyNode())
	{
	}

	public CadBlockDynamicBlockProxyNodeTemplate(BlockDynamicBlockProxyNode node)
		: base(node)
	{
	}
}
