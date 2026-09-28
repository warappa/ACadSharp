using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal partial class CadBlockVerticalConstraintParameterTemplate : CadBlock2PtParameterTemplate
{
	public BlockVerticalConstraintParameter BlockVerticalConstraintParameter { get { return this.CadObject as BlockVerticalConstraintParameter; } }

	public CadBlockVerticalConstraintParameterTemplate() : base(new BlockVerticalConstraintParameter())
	{
	}

	public CadBlockVerticalConstraintParameterTemplate(BlockVerticalConstraintParameter parameter)
		: base(parameter)
	{
	}
}
