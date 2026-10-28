// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.Assets;

/// <summary>The faces of an OBJ file that share one material.</summary>
internal sealed class ObjSubMesh
{
    public string Material;
    public readonly List<int> Triangles = new List<int>();
}

/// <summary>
/// A parsed OBJ file. Vertices are already unique per position, uv and normal combination, so the
/// arrays can go straight into a mesh. <see cref="Uvs"/> and <see cref="Normals"/> are empty when the
/// file didn't give them for every corner.
/// </summary>
internal sealed class ObjData
{
    public float[] Positions = new float[0];
    public float[] Uvs = new float[0];
    public float[] Normals = new float[0];
    public readonly List<ObjSubMesh> SubMeshes = new List<ObjSubMesh>();
    public string MtlLib;
    public readonly List<string> Warnings = new List<string>();

    public int VertexCount => Positions.Length / 3;

    public int TriangleCount
    {
        get
        {
            int count = 0;
            foreach (var sub in SubMeshes)
                count += sub.Triangles.Count / 3;
            return count;
        }
    }
}
