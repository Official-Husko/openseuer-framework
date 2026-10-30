// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Text;

namespace ObenFramework.SceneLook;

/// <summary>
/// Reads the hierarchy of a serialized scene: game objects, transforms, paths, and which body a renderer
/// belongs to. The layouts are those of Unity 6000.0 without type trees.
/// </summary>
internal static partial class SceneLookReader
{
    public static SceneLookGameObject GameObject(SceneLookFile f, long pathId)
    {
        if (f.Memo.GameObjects.TryGetValue(pathId, out var cached))
            return cached;
        var go = ParseGameObject(pathId, f.ReadObject(pathId));
        f.Memo.GameObjects[pathId] = go;
        return go;
    }

    public static SceneLookTransform Transform(SceneLookFile f, long pathId)
    {
        if (f.Memo.Transforms.TryGetValue(pathId, out var cached))
            return cached;
        var transform = ParseTransform(pathId, f.ReadObject(pathId));
        f.Memo.Transforms[pathId] = transform;
        return transform;
    }

    /// <summary>The path id of a game object's transform, or 0.</summary>
    public static long TransformOf(SceneLookFile f, long goPathId)
    {
        foreach (var component in GameObject(f, goPathId).Components)
        {
            if (component.FileId == 0 && f.Contains(component.PathId) && f.ClassId(component.PathId) == SceneLookClass.Transform)
                return component.PathId;
        }
        return 0;
    }

    /// <summary>The object and its parents up to the root, nearest first.</summary>
    public static List<long> Chain(SceneLookFile f, long goPathId)
    {
        var chain = new List<long>();
        long current = goPathId;
        while (current != 0 && !chain.Contains(current))
        {
            chain.Add(current);
            current = ParentOf(f, current);
        }
        return chain;
    }

    /// <summary>The transforms below a game object, itself included.</summary>
    public static List<long> SubtreeTransforms(SceneLookFile f, long goPathId)
    {
        var found = new List<long>();
        var stack = new List<long>();
        var visited = new HashSet<long>();
        long top = TransformOf(f, goPathId);
        if (top != 0)
            stack.Add(top);
        while (stack.Count > 0)
        {
            long current = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            if (!visited.Add(current))
                continue;
            found.Add(current);
            foreach (var child in Transform(f, current).Children)
            {
                if (child.FileId == 0 && f.Contains(child.PathId))
                    stack.Add(child.PathId);
            }
        }
        return found;
    }

    /// <summary>The game objects below a game object, itself included.</summary>
    public static List<long> Subtree(SceneLookFile f, long goPathId)
    {
        var objects = new List<long>();
        foreach (long transform in SubtreeTransforms(f, goPathId))
            objects.Add(Transform(f, transform).GameObject.PathId);
        return objects;
    }

    /// <summary>The hierarchy path from the scene root, names joined by '/'.</summary>
    public static string PathName(SceneLookFile f, long goPathId)
    {
        var chain = Chain(f, goPathId);
        var path = new StringBuilder();
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (i < chain.Count - 1)
                path.Append('/');
            path.Append(GameObject(f, chain[i]).Name);
        }
        return path.ToString();
    }

    /// <summary>What a game object carries that matters for looks: script class names, "Animator", "SMR".</summary>
    public static HashSet<string> Kinds(SceneLookFile f, long goPathId)
    {
        if (f.Memo.Kinds.TryGetValue(goPathId, out var cached))
            return cached;
        var kinds = new HashSet<string>();
        foreach (var component in GameObject(f, goPathId).Components)
        {
            if (component.FileId != 0 || !f.Contains(component.PathId))
                continue;
            int classId = f.ClassId(component.PathId);
            if (classId == SceneLookClass.MonoBehaviour)
                kinds.Add(BehaviourClass(f, component.PathId));
            else if (classId == SceneLookClass.Animator)
                kinds.Add("Animator");
            else if (classId == SceneLookClass.SkinnedMeshRenderer)
                kinds.Add("SMR");
        }
        f.Memo.Kinds[goPathId] = kinds;
        return kinds;
    }

    /// <summary>
    /// The body a game object belongs to: the nearest parent with an <c>NPCInfo</c>, otherwise the nearest
    /// with an Animator. 0 when there is neither.
    /// </summary>
    public static long BodyRoot(SceneLookFile f, long goPathId)
    {
        var chain = Chain(f, goPathId);
        foreach (long go in chain)
        {
            if (Kinds(f, go).Contains("NPCInfo"))
                return go;
        }
        foreach (long go in chain)
        {
            if (Kinds(f, go).Contains("Animator"))
                return go;
        }
        return 0;
    }

    /// <summary>The name of the game object a bone transform belongs to.</summary>
    public static string BoneName(SceneLookTarget bone)
    {
        long go = Transform(bone.File, bone.PathId).GameObject.PathId;
        return GameObject(bone.File, go).Name;
    }

    private static long ParentOf(SceneLookFile f, long goPathId)
    {
        long transform = TransformOf(f, goPathId);
        if (transform == 0)
            return 0;
        var father = Transform(f, transform).Father;
        if (father.FileId != 0 || !f.Contains(father.PathId))
            return 0;
        return Transform(f, father.PathId).GameObject.PathId;
    }

    internal static SceneLookGameObject ParseGameObject(long pathId, byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        var go = new SceneLookGameObject { PathId = pathId };
        int count = c.Count();
        go.Components = new List<SceneLookRef>(Math.Min(count, 64));
        for (int i = 0; i < count; i++)
            go.Components.Add(c.Pptr());
        c.I32(); // layer
        go.Name = c.Text();
        if (c.Position + 3 <= bytes.Length)
        {
            c.U16(); // tag
            go.Active = c.U8() != 0;
        }
        return go;
    }

    internal static SceneLookTransform ParseTransform(long pathId, byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        var transform = new SceneLookTransform
        {
            PathId = pathId,
            GameObject = c.Pptr(),
            Rotation = c.Floats(4),
            Position = c.Floats(3),
            Scale = c.Floats(3),
        };
        int count = c.Count();
        transform.Children = new List<SceneLookRef>(Math.Min(count, 64));
        for (int i = 0; i < count; i++)
            transform.Children.Add(c.Pptr());
        transform.Father = c.Pptr();
        return transform;
    }
}
