// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>
/// Bind pose maths. A bind pose is the inverse of a bone's rest transform relative to its mesh, so bones'
/// rest transforms can be recovered from the meshes that use them, and new bind poses made for a skeleton
/// the mesh wasn't made for.
/// </summary>
internal static class SceneLookBind
{
    /// <summary>A bone's rest transform in its mesh's frame, from the file's bind pose.</summary>
    public static SceneLookMatrix RestWorld(float[] fileBindPose) => SceneLookMatrix.FromFile(fileBindPose).Inverse();

    public static SceneLookMatrix Local(SceneLookMatrix parentWorld, SceneLookMatrix world) => parentWorld.Inverse().Multiply(world);

    public static SceneLookMatrix BindFrame(SceneLookMatrix bodyWorld, SceneLookMatrix holderWorld) => bodyWorld.Inverse().Multiply(holderWorld);

    public static SceneLookMatrix BindPose(SceneLookMatrix boneWorldRest, SceneLookMatrix rendererWorld) => boneWorldRest.Inverse().Multiply(rendererWorld);

    public static Dictionary<TKey, SceneLookMatrix> Rest<TKey>(IList<SceneLookBindMesh<TKey>> meshes, out int unresolved) =>
        Rest(meshes, null, out unresolved);

    /// <summary>
    /// The rest transform of every bone, in the frame of the first mesh that has bones. Other meshes are
    /// brought into that frame through a bone they share with what is already known, until nothing new is
    /// learned. <paramref name="unresolved"/> counts bone slots of meshes that share no bone with the rest.
    /// <paramref name="sources"/>, when given, records which mesh each bone came from.
    /// </summary>
    public static Dictionary<TKey, SceneLookMatrix> Rest<TKey>(IList<SceneLookBindMesh<TKey>> meshes, Dictionary<TKey, int> sources, out int unresolved)
    {
        var rest = new Dictionary<TKey, SceneLookMatrix>();
        unresolved = 0;
        if (meshes == null)
            return rest;
        int reference = -1;
        for (int i = 0; i < meshes.Count; i++)
        {
            if (meshes[i].Bones.Count == 0)
                continue;
            reference = i;
            break;
        }
        if (reference < 0)
            return rest;

        var first = meshes[reference];
        for (int i = 0; i < first.Bones.Count; i++)
        {
            if (rest.ContainsKey(first.Bones[i]))
                continue;
            rest[first.Bones[i]] = first.Poses[i].Inverse();
            if (sources != null)
                sources[first.Bones[i]] = reference;
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int m = 0; m < meshes.Count; m++)
            {
                var mesh = meshes[m];
                var frame = Frame(mesh, rest, out bool known);
                if (!known)
                    continue;
                for (int i = 0; i < mesh.Bones.Count; i++)
                {
                    if (rest.ContainsKey(mesh.Bones[i]))
                        continue;
                    rest[mesh.Bones[i]] = frame.Multiply(mesh.Poses[i].Inverse());
                    if (sources != null)
                        sources[mesh.Bones[i]] = m;
                    changed = true;
                }
            }
        }

        foreach (var mesh in meshes)
        {
            foreach (var bone in mesh.Bones)
            {
                if (!rest.ContainsKey(bone))
                    unresolved++;
            }
        }
        return rest;
    }

    /// <summary>A mesh's frame in the reference frame, through the first of its bones already known.</summary>
    public static SceneLookMatrix Frame<TKey>(SceneLookBindMesh<TKey> mesh, Dictionary<TKey, SceneLookMatrix> rest, out bool known)
    {
        known = false;
        for (int i = 0; i < mesh.Bones.Count; i++)
        {
            if (!rest.TryGetValue(mesh.Bones[i], out var bone))
                continue;
            known = true;
            return bone.Multiply(mesh.Poses[i]);
        }
        return SceneLookMatrix.Identity();
    }
}
