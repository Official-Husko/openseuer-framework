using System;
using System.Collections.Generic;
using System.IO;
using ObenLoader.Config;
using ObenLoader.Logging;

namespace ObenLoader.Mods;

/// <summary>Finds mod folders under &lt;game&gt;/Mods. A mod folder is any folder with a mod.json.</summary>
internal static class ModDiscovery
{
    public const string ManifestFile = "mod.json";

    public static List<ModInfo> Scan(string modsDir, ModLogger log)
    {
        var mods = new List<ModInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string dir in Directory.GetDirectories(modsDir))
        {
            string manifestPath = Path.Combine(dir, ManifestFile);
            if (!File.Exists(manifestPath))
                continue;

            if (!JsonFile.TryRead(manifestPath, out ModManifest manifest, out var error))
            {
                log.Error($"Skipping {Path.GetFileName(dir)}: bad mod.json ({error?.Message})");
                continue;
            }

            if (string.IsNullOrEmpty(manifest.Id))
                manifest.Id = Path.GetFileName(dir);

            if (!seen.Add(manifest.Id))
            {
                log.Error($"Skipping {dir}: another mod already uses the id '{manifest.Id}'");
                continue;
            }

            mods.Add(new ModInfo(manifest, dir));
        }

        return mods;
    }

    /// <summary>The DLLs that make up the mod's code (explicit list, or every DLL in the folder root).</summary>
    public static IEnumerable<string> AssemblyPaths(ModInfo mod)
    {
        if (mod.Manifest.Assemblies.Count == 0)
            return Directory.GetFiles(mod.Directory, "*.dll", SearchOption.TopDirectoryOnly);

        var paths = new List<string>(mod.Manifest.Assemblies.Count);
        foreach (string relative in mod.Manifest.Assemblies)
            paths.Add(Path.Combine(mod.Directory, relative));
        return paths;
    }

    /// <summary>
    /// DLLs in the folder root that aren't in an explicit assembly list: libraries the mod uses (a
    /// networking library, say). They load on first use and are never reloaded, so objects made from
    /// them can outlive a hot reload of the mod.
    /// </summary>
    public static IEnumerable<string> LibraryPaths(ModInfo mod)
    {
        if (mod.Manifest.Assemblies.Count == 0)
            yield break;

        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in AssemblyPaths(mod))
            listed.Add(Path.GetFullPath(path));

        foreach (string path in Directory.GetFiles(mod.Directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            if (!listed.Contains(Path.GetFullPath(path)))
                yield return path;
        }
    }
}
