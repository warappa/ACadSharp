using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal class CadBlockHandleParameterTemplate : CadBlockParameterTemplate
{
	public BlockHandleParameter BlockHandleParameter { get { return this.CadObject as BlockHandleParameter; } }

	public CadBlockHandleParameterTemplate() : base(new BlockHandleParameter())
	{
	}

	public CadBlockHandleParameterTemplate(BlockHandleParameter cadObject)
		: base(cadObject)
	{
	}
}
