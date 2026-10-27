using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ObenLoader.Mods;

/// <summary>
/// Loads a mod's DLLs. Hot-reload mods are read into memory (so the files stay unlocked for
/// rebuilding) and, once anything has been reloaded, rewritten so they bind to the newest copies
/// of the assemblies they use.
/// </summary>
internal static class AssemblyLoading
{
    private static readonly HashSet<Assembly> Stale = new HashSet<Assembly>();

    public static List<Assembly> Load(ModInfo mod)
    {
        var result = new List<Assembly>();
        foreach (string path in ModDiscovery.AssemblyPaths(mod))
        {
            // The first copy keeps its real name; every reload gets a fresh one.
            var assembly = mod.Manifest.HotReload ? FromMemory(path, rename: mod.LoadCount > 0) : Assembly.LoadFrom(path);
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

    private static Assembly FromMemory(string path, bool rename)
    {
        if (rename || AssemblyRewriter.Active)
        {
            var (image, symbols) = AssemblyRewriter.Prepare(path, rename);
            return symbols != null ? Assembly.Load(image, symbols) : Assembly.Load(image);
        }

        byte[] raw = File.ReadAllBytes(path);
        string pdb = Path.ChangeExtension(path, ".pdb");
        return File.Exists(pdb) ? Assembly.Load(raw, File.ReadAllBytes(pdb)) : Assembly.Load(raw);
    }
}
