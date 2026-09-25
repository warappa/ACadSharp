using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal class CadBlockTextParameterTemplate : CadBlockParameterTemplate
{
	public BlockTextParameter BlockTextParameter { get { return this.CadObject as BlockTextParameter; } }

	public CadBlockTextParameterTemplate() : base(new BlockTextParameter())
	{
	}

	public CadBlockTextParameterTemplate(BlockTextParameter cadObject)
		: base(cadObject)
	{
	}
}
