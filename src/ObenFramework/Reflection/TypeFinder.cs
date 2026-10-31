using System;
using System.Collections.Generic;
using System.Linq;

namespace ObenFramework.Reflection;

/// <summary>
/// Finds types by full or short name across every loaded assembly. Game types win over
/// same-named types elsewhere. Results are cached, and so are misses until another assembly loads.
/// </summary>
public static class TypeFinder
{
    private static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>(StringComparer.Ordinal);

    // Names nothing matched, with how many assemblies were loaded then: a watch on a type that's gone would
    // otherwise search every assembly on each redraw.
    private static readonly Dictionary<string, int> Misses = new Dictionary<string, int>(StringComparer.Ordinal);

    public static Type Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        // A mod that hot reloaded since leaves its old types behind; look those up again.
        if (Cache.TryGetValue(name, out var cached) && !ObenLoader.LoadedAssemblies.IsStale(cached.Assembly))
            return cached;

        int loaded = AppDomain.CurrentDomain.GetAssemblies().Length;
        if (Misses.TryGetValue(name, out int seen) && seen == loaded)
            return null;

        var type = Type.GetType(name, false) ?? Search(name);
        if (type != null)
        {
            Cache[name] = type;
            Misses.Remove(name);
        }
        else
        {
            Misses[name] = loaded;
        }
        return type;
    }

    public static Type Require(string name) =>
        Find(name) ?? throw new ArgumentException($"No type named '{name}'.");

    private static Type Search(string name)
    {
        Type shortMatch = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(Priority))
        {
            // Old copies left behind by hot reloads would shadow the running types.
            if (ObenLoader.LoadedAssemblies.IsStale(assembly))
                continue;

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }

            foreach (var type in types)
            {
                if (type == null)
                    continue;
                if (type.FullName == name)
                    return type;
                if (shortMatch == null && type.Name == name)
                    shortMatch = type;
            }
        }
        return shortMatch;
    }

    private static int Priority(System.Reflection.Assembly a)
    {
        string n = a.GetName().Name;
        if (n == "Assembly-CSharp") return 0;
        if (n.StartsWith("UnityEngine")) return 1;
        return 2;
    }
}
