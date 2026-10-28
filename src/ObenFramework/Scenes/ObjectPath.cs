// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Scenes;

/// <summary>
/// Plain hierarchy paths such as <c>Bazaar/Trader/Muscle Max</c>: splitting them and walking a tree of
/// any node type by name. When siblings share a name every one of them is tried, so a path still finds
/// an object whose first namesake has no matching child. Unity-free so the rules can be tested.
/// </summary>
public static class ObjectPath
{
    /// <summary>The names in <paramref name="path"/>, or null when it is empty or has an empty segment.</summary>
    public static string[] Split(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        string[] names = path.Split('/');
        foreach (string name in names)
        {
            if (name.Length == 0)
                return null;
        }
        return names;
    }

    /// <summary>
    /// Finds the first node, depth first, whose chain of names from one of <paramref name="nodes"/> matches
    /// <paramref name="names"/> starting at <paramref name="depth"/>. Nodes that fail
    /// <paramref name="visible"/> are skipped together with their children.
    /// </summary>
    public static T Descend<T>(IEnumerable<T> nodes, string[] names, int depth, Func<T, string> nameOf,
        Func<T, bool> visible, Func<T, IEnumerable<T>> childrenOf) where T : class
    {
        if (names == null || depth >= names.Length)
            return null;
        foreach (T node in nodes)
        {
            if (nameOf(node) != names[depth] || !visible(node))
                continue;
            if (depth == names.Length - 1)
                return node;
            T found = Descend(childrenOf(node), names, depth + 1, nameOf, visible, childrenOf);
            if (found != null)
                return found;
        }
        return null;
    }
}
