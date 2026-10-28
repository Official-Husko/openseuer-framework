// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>Where the parts of a look sit relative to its root, from the file's transforms.</summary>
internal static class SceneLookFrame
{
    public const int MaxDepth = 256;
    public const float MinScale = 0.0001f;

    public static SceneLookNode Node(SceneLookLook look, long gameObject)
    {
        foreach (var node in look.Nodes)
        {
            if (node.GameObject == gameObject)
                return node;
        }
        return null;
    }

    public static Dictionary<long, SceneLookNode> ByTransform(SceneLookLook look)
    {
        var map = new Dictionary<long, SceneLookNode>(look.Nodes.Count);
        foreach (var node in look.Nodes)
            map[node.Transform] = node;
        return map;
    }

    public static SceneLookMatrix Local(SceneLookNode node) => SceneLookMatrix.Trs(node.Position, node.Rotation, node.Scale);

    /// <summary>A node's transform relative to the look root's parent (the root's own transform included).</summary>
    public static SceneLookMatrix Relative(SceneLookLook look, SceneLookNode node, Dictionary<long, SceneLookNode> byTransform)
    {
        var result = Local(node);
        var current = node;
        int depth = 0;
        while (current != null && current.GameObject != look.Root && depth < MaxDepth)
        {
            var parent = Parent(current, byTransform);
            if (parent == null)
                break;
            result = Local(parent).Multiply(result);
            current = parent;
            depth++;
        }
        return result;
    }

    /// <summary>A node's transform relative to the look root (the root's own transform left out).</summary>
    public static SceneLookMatrix RelativeWithoutRoot(SceneLookLook look, SceneLookNode node, Dictionary<long, SceneLookNode> byTransform)
    {
        if (node.GameObject == look.Root)
            return SceneLookMatrix.Identity();
        var result = Local(node);
        var current = node;
        int depth = 0;
        while (current != null && current.GameObject != look.Root && depth < MaxDepth)
        {
            var parent = Parent(current, byTransform);
            if (parent == null || parent.GameObject == look.Root)
                break;
            result = Local(parent).Multiply(result);
            current = parent;
            depth++;
        }
        return result;
    }

    /// <summary>The first renderer with bones whose object is in the look's tree, or -1.</summary>
    public static int Seed(SceneLookLook look)
    {
        for (int i = 0; i < look.Renderers.Count; i++)
        {
            var renderer = look.Renderers[i];
            if (renderer?.Source == null || renderer.Bones.Count == 0)
                continue;
            if (Node(look, renderer.Source.GameObject.PathId) != null)
                return i;
        }
        return -1;
    }

    public static SceneLookNode Reference(SceneLookLook look)
    {
        int seed = Seed(look);
        return seed < 0 ? null : Node(look, look.Renderers[seed].Source.GameObject.PathId);
    }

    /// <summary>The renderers whose objects are in the look's tree.</summary>
    public static List<SceneLookLookRenderer> RestRenderers(SceneLookLook look)
    {
        var renderers = new List<SceneLookLookRenderer>(look.Renderers.Count);
        foreach (var renderer in look.Renderers)
        {
            if (renderer?.Source == null || Node(look, renderer.Source.GameObject.PathId) == null)
                continue;
            renderers.Add(renderer);
        }
        return renderers;
    }

    /// <summary>The bones and file bind poses of each renderer, keyed by bone transform path id.</summary>
    public static List<SceneLookBindMesh<long>> RestMeshes(SceneLookLook look)
    {
        var renderers = RestRenderers(look);
        var meshes = new List<SceneLookBindMesh<long>>(renderers.Count);
        foreach (var renderer in renderers)
        {
            var mesh = new SceneLookBindMesh<long>();
            for (int i = 0; i < renderer.Bones.Count; i++)
            {
                var pose = SceneLookMatrix.Identity();
                if (renderer.Mesh?.BindPoses != null && i < renderer.Mesh.BindPoses.Length)
                    pose = SceneLookMatrix.FromFile(renderer.Mesh.BindPoses[i]);
                mesh.Add(renderer.Bones[i].Transform.PathId, pose);
            }
            meshes.Add(mesh);
        }
        return meshes;
    }

    /// <summary>Where the holder of a retargeted look goes: the reference renderer's place under the root.</summary>
    public static SceneLookMatrix Holder(SceneLookLook look, Dictionary<long, SceneLookNode> byTransform)
    {
        var reference = Reference(look);
        return reference == null ? SceneLookMatrix.Identity() : RelativeWithoutRoot(look, reference, byTransform);
    }

    /// <summary>The mean absolute scale of a scale vector; 1 when there is none.</summary>
    public static float Magnitude(float[] scale)
    {
        if (scale == null || scale.Length < 3)
            return 1f;
        return (Math.Abs(scale[0]) + Math.Abs(scale[1]) + Math.Abs(scale[2])) / 3f;
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float RootScale(SceneLookLook look)
    {
        var root = Node(look, look.Root);
        return root == null ? 1f : Magnitude(root.Scale);
    }

    /// <summary>How much to scale the holder so the look matches a body of <paramref name="targetScale"/>.</summary>
    public static bool HolderRatio(SceneLookLook look, float targetScale, out float factor, out string reason)
    {
        factor = 1f;
        reason = null;
        if (!Finite(targetScale) || !(targetScale > MinScale))
        {
            reason = "the target body scale is zero or not a finite number";
            return false;
        }
        float root = RootScale(look);
        if (!Finite(root) || !(root > MinScale))
        {
            reason = "the look root scale is zero or not a finite number";
            return false;
        }
        factor = root / targetScale;
        if (!Finite(factor) || !(factor > 0f))
        {
            factor = 1f;
            reason = "the scale ratio is not a finite number above zero";
            return false;
        }
        return true;
    }

    /// <summary>The mean length of a matrix's three axes.</summary>
    public static float Scale(SceneLookMatrix matrix)
    {
        double x = Length(matrix.M00, matrix.M10, matrix.M20);
        double y = Length(matrix.M01, matrix.M11, matrix.M21);
        double z = Length(matrix.M02, matrix.M12, matrix.M22);
        return (float)((x + y + z) / 3.0);
    }

    public static SceneLookNode Parent(SceneLookNode node, Dictionary<long, SceneLookNode> byTransform)
    {
        if (node.Father.FileId != 0)
            return null;
        return byTransform.TryGetValue(node.Father.PathId, out var parent) ? parent : null;
    }

    private static double Length(float x, float y, float z) => Math.Sqrt((double)x * x + (double)y * y + (double)z * z);
}
