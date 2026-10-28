// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Patching;

/// <summary>
/// Which patch areas each mod has and whether they still stand. An area is switched off as a whole when
/// one of its patches fails: every patch applied in it so far is undone, and later ones are skipped, so a
/// feature never runs half patched. Plain bookkeeping with the undo steps passed in, so it is tested on
/// its own.
/// </summary>
internal sealed class PatchAreaBook
{
    private sealed class Area
    {
        public string Owner;
        public string Name;
        public int Patches;
        public string Error;
        public readonly List<Action> Undo = new List<Action>();
    }

    private readonly Dictionary<string, Area> _areas = new Dictionary<string, Area>(StringComparer.OrdinalIgnoreCase);
    private readonly List<Area> _order = new List<Area>();

    /// <summary>True when the area was switched off. Areas never seen are available.</summary>
    public bool IsDisabled(string owner, string area) =>
        _areas.TryGetValue(Key(owner, area), out var entry) && entry.Error != null;

    /// <summary>Records a patch applied in an area, with how to take it off again.</summary>
    public void Applied(string owner, string area, Action undo)
    {
        var entry = Get(owner, area);
        entry.Patches++;
        if (undo != null)
            entry.Undo.Add(undo);
    }

    /// <summary>Makes sure the area is listed, even before anything was applied in it.</summary>
    public void Touch(string owner, string area) => Get(owner, area);

    /// <summary>
    /// Switches an area off: undoes its patches newest first and remembers why. Returns false when it
    /// was already off. An undo step that throws is passed to <paramref name="undoFailed"/> and the rest
    /// still run.
    /// </summary>
    public bool Fail(string owner, string area, string reason, Action<Exception> undoFailed = null)
    {
        var entry = Get(owner, area);
        if (entry.Error != null)
            return false;
        entry.Error = string.IsNullOrEmpty(reason) ? "a patch failed" : reason;
        for (int i = entry.Undo.Count - 1; i >= 0; i--)
        {
            try { entry.Undo[i](); }
            catch (Exception e) { undoFailed?.Invoke(e); }
        }
        entry.Undo.Clear();
        return true;
    }

    /// <summary>Drops everything about one mod's areas (it unloaded; its Harmony patches went with it).</summary>
    public void Forget(string owner)
    {
        _order.RemoveAll(a => string.Equals(a.Owner, owner, StringComparison.OrdinalIgnoreCase));
        var keys = new List<string>();
        foreach (var pair in _areas)
        {
            if (string.Equals(pair.Value.Owner, owner, StringComparison.OrdinalIgnoreCase))
                keys.Add(pair.Key);
        }
        foreach (string key in keys)
            _areas.Remove(key);
    }

    /// <summary>The areas in the order they were first seen; all of them, or only <paramref name="owner"/>'s.</summary>
    public List<PatchAreaInfo> Snapshot(string owner = null)
    {
        var list = new List<PatchAreaInfo>(_order.Count);
        foreach (var area in _order)
        {
            if (owner != null && !string.Equals(area.Owner, owner, StringComparison.OrdinalIgnoreCase))
                continue;
            list.Add(new PatchAreaInfo
            {
                Owner = area.Owner,
                Name = area.Name,
                Patches = area.Patches,
                IsAvailable = area.Error == null,
                Error = area.Error,
            });
        }
        return list;
    }

    private Area Get(string owner, string area)
    {
        string key = Key(owner, area);
        if (!_areas.TryGetValue(key, out var entry))
        {
            entry = new Area { Owner = owner, Name = area };
            _areas[key] = entry;
            _order.Add(entry);
        }
        return entry;
    }

    private static string Key(string owner, string area) => (owner ?? "") + "\n" + (area ?? "");

    /// <summary>The namespace part below the root ("ObenFramework.World.Logic" under "ObenFramework" is "World.Logic"), or "Main".</summary>
    public static string AreaFromNamespace(string ns, string rootNamespace)
    {
        if (string.IsNullOrEmpty(ns))
            return "Main";
        if (!string.IsNullOrEmpty(rootNamespace))
        {
            if (string.Equals(ns, rootNamespace, StringComparison.Ordinal))
                return "Main";
            if (ns.StartsWith(rootNamespace + ".", StringComparison.Ordinal))
                return ns.Substring(rootNamespace.Length + 1);
        }
        return ns;
    }
}
