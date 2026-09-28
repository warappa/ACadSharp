using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal partial class CadBlockUserParameterTemplate : CadBlockParameterTemplate
{
	public BlockUserParameter BlockUserParameter { get { return this.CadObject as BlockUserParameter; } }

	public CadBlockUserParameterTemplate() : base(new BlockUserParameter())
	{
	}

	public CadBlockUserParameterTemplate(BlockUserParameter parameter)
		: base(parameter)
	{
	}
}
