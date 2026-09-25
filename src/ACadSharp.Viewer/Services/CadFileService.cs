using ACadSharp;
using ACadSharp.IO;
using System.IO;

namespace ACadSharp.Viewer.Services;

/// <summary>
/// Loads a .dwg or .dxf file into a <see cref="CadDocument"/>.
/// </summary>
public static class CadFileService
{
    public static CadDocument LoadFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".dxf")
        {
            using (DxfReader reader = new DxfReader(path))
            {
                return reader.Read();
            }
        }

        using (DwgReader reader = new DwgReader(path))
        {
            return reader.Read();
        }
    }
}
