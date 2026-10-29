using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ObenLoader.Config;

namespace ObenLoader.Mods;

/// <summary>Reloading mods in place. Everything in the set must support hot reload.</summary>
internal static partial class ModLifecycle
{
    public static string Reload(ModInfo mod) => ReloadSet(new[] { mod });

    /// <summary>
    /// Reloads several mods and, in a cascade, every loaded mod built on them. Dependents unload first
    /// (newest first) and everything loads back in dependency order as one batch.
    /// </summary>
    public static string ReloadSet(IEnumerable<ModInfo> roots)
    {
        var set = new List<ModInfo>();
        foreach (var root in roots)
        {
            if (!set.Contains(root))
                set.Add(root);
            foreach (var dependent in LoadedDependentsDeep(root))
            {
                if (!set.Contains(dependent))
                    set.Add(dependent);
            }
        }

        var blocked = set.Where(m => !m.CanHotReload).Select(m => m.Id).ToList();
        if (blocked.Count > 0)
            return $"Can't reload without a restart: {string.Join(", ", blocked)} doesn't support hot reload.";

        var timer = Stopwatch.StartNew();
        var order = DependencySorter.Sort(set.Where(m => m.State != ModState.Failed).ToList());
        for (int i = order.Count - 1; i >= 0; i--)
        {
            if (order[i].State != ModState.Loaded)
                continue;
            Unloading?.Invoke(order[i]);
            ModActivator.Deactivate(order[i], _host, _log);
        }

        foreach (var mod in set)
        {
            RefreshManifest(mod);
            mod.State = ModState.Pending;
            mod.Error = null;
        }
        LoadBatch(set, atRuntime: true);

        var failed = set.Where(m => m.State != ModState.Loaded).ToList();
        if (failed.Count > 0)
            return string.Join("\n", failed.Select(m => $"{m.Id} failed to load: {m.Error}"));

        _log.Info($"Reloaded {string.Join(", ", set.Select(m => m.Id))} in {timer.ElapsedMilliseconds} ms");
        return null;
    }

    /// <summary>Dependents, their dependents, and so on.</summary>
    private static List<ModInfo> LoadedDependentsDeep(ModInfo mod)
    {
        var found = new List<ModInfo>();
        var queue = new Queue<ModInfo>();
        queue.Enqueue(mod);
        while (queue.Count > 0)
        {
            foreach (var dependent in LoadedDependents(queue.Dequeue()))
            {
                if (found.Contains(dependent))
                    continue;
                found.Add(dependent);
                queue.Enqueue(dependent);
            }
        }
        return found;
    }

    private static void RefreshManifest(ModInfo mod)
    {
        string path = Path.Combine(mod.Directory, ModDiscovery.ManifestFile);
        ModManifest manifest = null;
        if (!mod.IsLoose && File.Exists(path))
            JsonFile.TryRead(path, out manifest, out _);
        else if (mod.Manifest.Assemblies.Count > 0)
        {
            // Declared in code: the attribute may have changed with the rebuild.
            string dll = Path.Combine(mod.Directory, mod.Manifest.Assemblies[0]);
            manifest = mod.IsLoose ? CodeManifest.ReadLoose(dll, null) : CodeManifest.ReadAssembly(dll, null);
        }
        if (manifest != null)
        {
            manifest.Id = mod.Id; // The id can't change while running.
            mod.Manifest = manifest;
        }
    }
}
