using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal partial class CadBlockHorizontalConstraintParameterTemplate : CadBlock2PtParameterTemplate
{
	public BlockHorizontalConstraintParameter BlockHorizontalConstraintParameter { get { return this.CadObject as BlockHorizontalConstraintParameter; } }

	public CadBlockHorizontalConstraintParameterTemplate() : base(new BlockHorizontalConstraintParameter())
	{
	}

	public CadBlockHorizontalConstraintParameterTemplate(BlockHorizontalConstraintParameter parameter)
		: base(parameter)
	{
	}
}
