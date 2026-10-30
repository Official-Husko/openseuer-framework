// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>
/// The part of a look's node tree its avatar is built from: the bones and every node above them. The rest
/// (footstep audio, the clothes' own objects) may share names; the rebuilt skeleton numbers those apart.
/// </summary>
internal static class SceneLookChain
{
    /// <summary>The look's bones and every node above them up to its root, by transform.</summary>
    public static HashSet<long> Bones(SceneLookLook look)
    {
        var chain = new HashSet<long>();
        if (look.Nodes == null || look.Renderers == null)
            return chain;
        var byTransform = new Dictionary<long, SceneLookNode>(look.Nodes.Count);
        foreach (var node in look.Nodes)
            byTransform[node.Transform] = node;
        foreach (var renderer in look.Renderers)
        {
            foreach (var bone in renderer.Bones)
            {
                long current = bone.Transform.PathId;
                while (byTransform.TryGetValue(current, out var node) && chain.Add(current) && node.Father.FileId == 0)
                    current = node.Father.PathId;
            }
        }
        return chain;
    }

    /// <summary>A name for each look node in the rebuilt skeleton: the bone chain keeps its own, any other node clashing gets a number.</summary>
    public static Dictionary<long, string> Names(SceneLookLook look)
    {
        var names = new Dictionary<long, string>();
        if (look.Nodes == null)
            return names;
        var chain = Bones(look);
        var used = new HashSet<string>();
        foreach (var node in look.Nodes)
        {
            if (chain.Contains(node.Transform) || node.GameObject == look.Root)
            {
                names[node.Transform] = node.Name;
                used.Add(node.Name);
            }
        }
        foreach (var node in look.Nodes)
        {
            if (names.ContainsKey(node.Transform))
                continue;
            string name = node.Name;
            for (int n = 2; !used.Add(name); n++)
                name = node.Name + " (" + n + ")";
            names[node.Transform] = name;
        }
        return names;
    }

}
