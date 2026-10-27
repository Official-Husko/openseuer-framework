using System.Collections.Generic;
using System.Linq;

namespace ObenLoader.Mods;

/// <summary>
/// Orders mods so dependencies load first. Mods with missing or broken dependencies, or caught in
/// a cycle, are marked failed and left out.
/// </summary>
internal static class DependencySorter
{
    public static List<ModInfo> Sort(IReadOnlyList<ModInfo> candidates)
    {
        var byId = candidates.ToDictionary(m => m.Id);
        CheckRequirements(candidates, byId);

        var ordered = new List<ModInfo>(candidates.Count);
        var visiting = new HashSet<string>();
        var done = new HashSet<string>();

        // Alphabetical start keeps the order stable between launches.
        foreach (var mod in candidates.OrderBy(m => m.Id))
            Visit(mod, byId, visiting, done, ordered);

        return ordered;
    }

    private static void CheckRequirements(IReadOnlyList<ModInfo> candidates, Dictionary<string, ModInfo> byId)
    {
        // Repeat until nothing changes: one failure can take down mods that depend on it.
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var mod in candidates)
            {
                if (mod.State == ModState.Failed)
                    continue;
                foreach (var dep in mod.Manifest.Dependencies)
                {
                    string problem = DependencyProblem(dep.Key, dep.Value, byId);
                    if (problem == null)
                        continue;
                    mod.Fail(problem);
                    changed = true;
                    break;
                }
            }
        }
    }

    private static string DependencyProblem(string id, string minVersion, Dictionary<string, ModInfo> byId)
    {
        // Outside this batch: fine if it's already running (runtime loads and reloads).
        if (!byId.TryGetValue(id, out var dep))
        {
            var running = ModRegistry.Find(id);
            if (running == null || running.State != ModState.Loaded)
                return $"Needs '{id}', which isn't installed or is switched off.";
            dep = running;
        }
        if (dep.State == ModState.Failed)
            return $"Needs '{id}', which failed to load.";
        if (!SemVer.Satisfies(dep.Manifest.Version, minVersion))
            return $"Needs '{id}' {minVersion} or newer (found {dep.Manifest.Version}).";
        return null;
    }

    private static void Visit(ModInfo mod, Dictionary<string, ModInfo> byId, HashSet<string> visiting,
        HashSet<string> done, List<ModInfo> ordered)
    {
        if (mod.State == ModState.Failed || done.Contains(mod.Id))
            return;
        if (!visiting.Add(mod.Id))
        {
            mod.Fail("Circular dependency.");
            return;
        }

        foreach (string id in mod.Manifest.Dependencies.Keys)
        {
            if (byId.TryGetValue(id, out var dep))
                Visit(dep, byId, visiting, done, ordered);
        }

        // loadAfter is only a hint: if following it would close a loop, skip it instead of failing.
        foreach (string id in mod.Manifest.LoadAfter)
        {
            if (byId.TryGetValue(id, out var hint) && !visiting.Contains(hint.Id))
                Visit(hint, byId, visiting, done, ordered);
        }

        visiting.Remove(mod.Id);
        done.Add(mod.Id);

        // A dependency may have failed during the visit above.
        foreach (string id in mod.Manifest.Dependencies.Keys)
        {
            if (byId.TryGetValue(id, out var dep) && dep.State == ModState.Failed)
            {
                mod.Fail($"Needs '{id}', which failed to load.");
                return;
            }
        }

        ordered.Add(mod);
    }
}
