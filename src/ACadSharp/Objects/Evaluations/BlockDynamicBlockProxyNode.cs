using ACadSharp.Attributes;
using ACadSharp.Classes;

namespace ACadSharp.Objects.Evaluations;

/// <summary>
/// Represents an ACDB_DYNAMICBLOCKPROXYNODE object, a placeholder node in a dynamic block's
/// evaluation graph for a node type that is not understood (a proxy).
/// </summary>
/// <remarks>
/// Object name <see cref="DxfFileToken.ObjectDynamicBlockProxyNode"/> <br/>
/// Dxf class name <see cref="DxfSubclassMarker.BlockDynamicBlockProxyNode"/>
/// </remarks>
[DxfName(DxfFileToken.ObjectDynamicBlockProxyNode)]
[DxfSubClass(DxfSubclassMarker.BlockDynamicBlockProxyNode)]
public class BlockDynamicBlockProxyNode : EvaluationExpression, IDxfClassDefined
{
	/// <summary>
	/// The name of the proxied node type.
	/// </summary>
	[DxfCodeValue(300)]
	public string ProxyName { get; set; }

	/// <summary>
	/// The proxy data (an opaque payload).
	/// </summary>
	[DxfCodeValue(309)]
	public byte[] ProxyData { get; set; }

	/// <inheritdoc/>
	public override string ObjectName => DxfFileToken.ObjectDynamicBlockProxyNode;

	/// <inheritdoc/>
	public override string SubclassMarker => DxfSubclassMarker.BlockDynamicBlockProxyNode;

	/// <inheritdoc/>
	public DxfClass GetDxfClass()
	{
		return new DxfClass
		{
			CppClassName = DxfSubclassMarker.BlockDynamicBlockProxyNode,
			DwgVersion = ACadVersion.AC1018,
			DxfName = DxfFileToken.ObjectDynamicBlockProxyNode,
			ItemClassId = 499,
			MaintenanceVersion = 55,
			ProxyFlags = ACadSharp.Classes.ProxyFlags.EraseAllowed | ACadSharp.Classes.ProxyFlags.CloningAllowed | ACadSharp.Classes.ProxyFlags.DisablesProxyWarningDialog,
			WasZombie = false,
		};
	}
}
