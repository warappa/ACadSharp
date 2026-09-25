using ACadSharp.Objects.Evaluations;

namespace ACadSharp.IO.Templates;

internal class CadBlockCharParameterTemplate : CadBlockParameterTemplate
{
	public BlockCharParameter BlockCharParameter { get { return this.CadObject as BlockCharParameter; } }

	public CadBlockCharParameterTemplate() : base(new BlockCharParameter())
	{
	}

	public CadBlockCharParameterTemplate(BlockCharParameter cadObject)
		: base(cadObject)
	{
	}
}
