// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>A model read and laid out on a worker thread, waiting for the main thread to turn it into a mesh.</summary>
internal sealed class PreparedModel
{
    public string Key;
    public string Path;
    public string Error;
    public Vector3[] Vertices;
    public Vector3[] Normals;
    public Vector2[] Uvs;
    public int[][] Indices;
    public Bounds Bounds;
    public int Triangles;

    /// <summary>One entry per submesh; null where the OBJ named no material or the MTL didn't have it.</summary>
    public MtlMaterial[] Materials;

    public readonly List<string> Warnings = new List<string>();
}

/// <summary>
/// The part of loading a model that needs no Unity API: reading the OBJ and MTL, applying the options and
/// laying out the vertex arrays. Safe to run on any thread; nothing here touches an engine object.
/// </summary>
internal static class ModelPrepare
{
    public static PreparedModel Run(string key, string objPath, ModelOptions options)
    {
        var prepared = new PreparedModel { Key = key, Path = objPath };
        try
        {
            ObjData data = ObjParser.ParseFile(objPath, options.FlipX, out string error);
            if (data == null)
            {
                prepared.Error = error;
                return prepared;
            }

            MtlData mtl = LoadMtl(data, objPath, prepared.Warnings);
            prepared.Warnings.AddRange(data.Warnings);
            prepared.Warnings.AddRange(mtl.Warnings);

            var settings = new MeshSettings
            {
                Scale = options.Scale,
                EulerX = options.Rotation.x, EulerY = options.Rotation.y, EulerZ = options.Rotation.z,
                OffsetX = options.Offset.x, OffsetY = options.Offset.y, OffsetZ = options.Offset.z,
                Pivot = options.Pivot,
            };
            Lay(prepared, data, mtl, MeshPlan.Build(data, settings));
        }
        catch (Exception e)
        {
            prepared.Error = e.Message;
        }
        return prepared;
    }

    private static void Lay(PreparedModel prepared, ObjData data, MtlData mtl, MeshArrays arrays)
    {
        prepared.Vertices = ToVectors3(arrays.Positions);
        prepared.Normals = ToVectors3(arrays.Normals);
        if (arrays.Uvs != null)
        {
            prepared.Uvs = new Vector2[arrays.Uvs.Length / 2];
            for (int i = 0; i < prepared.Uvs.Length; i++)
                prepared.Uvs[i] = new Vector2(arrays.Uvs[i * 2], arrays.Uvs[i * 2 + 1]);
        }
        prepared.Indices = arrays.Indices;
        prepared.Triangles = data.TriangleCount;
        var min = new Vector3(arrays.Min[0], arrays.Min[1], arrays.Min[2]);
        var max = new Vector3(arrays.Max[0], arrays.Max[1], arrays.Max[2]);
        prepared.Bounds = new Bounds((min + max) * 0.5f, max - min);

        prepared.Materials = new MtlMaterial[data.SubMeshes.Count];
        for (int i = 0; i < prepared.Materials.Length; i++)
        {
            string name = data.SubMeshes[i].Material;
            prepared.Materials[i] = mtl.Find(name);
            if (name != null && prepared.Materials[i] == null && data.MtlLib != null)
                prepared.Warnings.Add("material " + name + " is not in " + data.MtlLib);
        }
    }

    private static Vector3[] ToVectors3(float[] values)
    {
        var result = new Vector3[values.Length / 3];
        for (int i = 0; i < result.Length; i++)
            result[i] = new Vector3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
        return result;
    }

    private static MtlData LoadMtl(ObjData data, string objPath, List<string> warnings)
    {
        if (data.MtlLib == null)
            return new MtlData();
        string name = data.MtlLib.Replace('\\', '/');
        if (name.Length == 0 || Path.IsPathRooted(name) || name.IndexOf("..", StringComparison.Ordinal) >= 0)
        {
            warnings.Add("mtllib " + data.MtlLib + " refused: it must be a file inside the model folder");
            return new MtlData();
        }
        string folder = Path.GetDirectoryName(Path.GetFullPath(objPath));
        return MtlParser.Parse(Path.Combine(folder, name), folder);
    }
}
