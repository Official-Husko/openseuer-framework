using System;
using System.Collections.Generic;
using System.IO;
using ObenLoader.Config;
using ObenLoader.Logging;

namespace ObenLoader.Mods;

/// <summary>
/// Finds mods under &lt;game&gt;/Mods: any folder with a mod.json, or without one, a folder whose DLL
/// declares the mod in code (<see cref="ObenModInfoAttribute"/>). DLLs lying loose in the Mods folder load
/// too, with a warning in the log.
/// </summary>
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
            ModManifest manifest;
            if (!File.Exists(manifestPath))
            {
                manifest = CodeManifest.Read(dir, log.Warn);
                if (manifest == null)
                    continue;
            }
            else if (!JsonFile.TryRead(manifestPath, out manifest, out var error))
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

        // After the folders, so a mod with a folder of its own wins over a stray copy of its DLL.
        foreach (string dll in Directory.GetFiles(modsDir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            string file = Path.GetFileName(dll);
            var manifest = CodeManifest.ReadLoose(dll, log.Warn);
            if (manifest == null)
            {
                log.Warn($"{file} lies loose in the Mods folder and isn't a mod (it doesn't use the loader); it isn't loaded.");
                continue;
            }
            if (!seen.Add(manifest.Id))
            {
                log.Error($"Skipping {file}: another mod already uses the id '{manifest.Id}'");
                continue;
            }
            log.Warn($"{file} lies loose in the Mods folder. It loads as '{manifest.Id}', but give it a folder of its own (Mods/{manifest.Id}/) with a mod.json; its settings and files go in that folder.");
            mods.Add(new ModInfo(manifest, Path.Combine(modsDir, manifest.Id)) { IsLoose = true });
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

        // A loose mod's folder may not exist, and the DLLs next to a loose DLL belong to other mods.
        if (mod.IsLoose || !Directory.Exists(mod.Directory))
            yield break;
        foreach (string path in Directory.GetFiles(mod.Directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            if (!listed.Contains(Path.GetFullPath(path)))
                yield return path;
        }
    }
}
