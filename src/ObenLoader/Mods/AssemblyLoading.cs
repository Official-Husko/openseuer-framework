using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ObenLoader.Mods;

/// <summary>Loads a mod's DLLs and keeps track of copies that were replaced.</summary>
internal static class AssemblyLoading
{
    private static readonly HashSet<Assembly> Stale = new HashSet<Assembly>();

    public static List<Assembly> Load(ModInfo mod)
    {
        var result = new List<Assembly>();
        foreach (string path in ModDiscovery.AssemblyPaths(mod))
        {
            var assembly = Assembly.LoadFrom(path);
            AssemblyResolver.SetCurrent(assembly, Path.GetFileNameWithoutExtension(path));
            result.Add(assembly);
        }
        return result;
    }

    /// <summary>Marks a mod's current assemblies as replaced (they stay in memory; Mono can't unload them).</summary>
    public static void Retire(ModInfo mod)
    {
        foreach (var assembly in mod.Assemblies)
            Stale.Add(assembly);
    }

    public static bool IsStale(Assembly assembly) => Stale.Contains(assembly);

    /// <summary>Newest write time across the mod's DLLs, used to spot rebuilds.</summary>
    public static System.DateTime LastWrite(ModInfo mod)
    {
        var newest = System.DateTime.MinValue;
        foreach (string path in ModDiscovery.AssemblyPaths(mod))
        {
            var t = File.GetLastWriteTimeUtc(path);
            if (t > newest)
                newest = t;
        }
        return newest;
    }
}
