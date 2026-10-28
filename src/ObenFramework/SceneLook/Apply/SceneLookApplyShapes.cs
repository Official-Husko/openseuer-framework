using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>
/// Adds a look mesh's blend shapes (face shapes, body morphs) to the rebuilt mesh and sets the weights the
/// scene gave its renderer, so the look keeps the expression and proportions it has in the level.
/// </summary>
internal static class SceneLookApplyShapes
{
    public static void Add(Mesh mesh, SceneLookMesh source)
    {
        var frames = SceneLookBlendShapes.Frames(source, out string reason);
        if (frames == null)
        {
            SceneLooks.Log.Warn("blend shapes " + source.Name, reason + "; the look is built without them");
            return;
        }
        if (frames.Count == 0)
            return;

        int count = (int)source.VertexCount;
        var positions = new float[count * 3];
        var normals = new float[count * 3];
        var tangents = new float[count * 3];
        var dv = new Vector3[count];
        var dn = new Vector3[count];
        var dt = new Vector3[count];
        foreach (var frame in frames)
        {
            if (!SceneLookBlendShapes.Fill(source, frame, positions, normals, tangents))
            {
                SceneLooks.Log.Warn("blend shape " + frame.Name, "a vertex index is outside the mesh; the shape is left out");
                continue;
            }
            for (int i = 0; i < count; i++)
            {
                int at = i * 3;
                dv[i] = new Vector3(positions[at], positions[at + 1], positions[at + 2]);
                dn[i] = new Vector3(normals[at], normals[at + 1], normals[at + 2]);
                dt[i] = new Vector3(tangents[at], tangents[at + 1], tangents[at + 2]);
            }
            try { mesh.AddBlendShapeFrame(frame.Name, frame.Weight, dv, frame.HasNormals ? dn : null, frame.HasTangents ? dt : null); }
            catch (System.Exception e) { SceneLooks.Log.Warn("blend shape " + frame.Name, "left out: " + e.Message); }
        }
    }

    /// <summary>Sets the renderer's blend shape weights to the ones the scene file had.</summary>
    public static void Weights(SkinnedMeshRenderer renderer, float[] weights)
    {
        if (weights == null || renderer.sharedMesh == null)
            return;
        int count = Mathf.Min(weights.Length, renderer.sharedMesh.blendShapeCount);
        for (int i = 0; i < count; i++)
            renderer.SetBlendShapeWeight(i, weights[i]);
    }
}
