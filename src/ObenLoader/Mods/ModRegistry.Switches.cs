using System.Collections.Generic;
using System.Linq;

namespace ObenLoader.Mods;

/// <summary>The player's on/off switches. They apply on the next launch.</summary>
public static partial class ModRegistry
{
    public static bool IsEnabled(string id)
    {
        var mod = Find(id);
        bool wanted = !_settings.Value.DisabledMods.Contains(id)
            && (mod == null || mod.Manifest.EnabledByDefault || _settings.Value.EnabledMods.Contains(id));
        return wanted || (mod != null && AlwaysOnReason(mod) != null);
    }

    /// <summary>Switches a mod on or off for the next launch. Returns why not, or null when done.</summary>
    public static string SetEnabled(string id, bool enabled)
    {
        var mod = Find(id);
        string reason = mod != null && !enabled ? AlwaysOnReason(mod) : null;
        if (reason != null)
            return $"Can't switch off {mod.Manifest.DisplayName}: {reason}.";

        var disabled = _settings.Value.DisabledMods;
        var switchedOn = _settings.Value.EnabledMods;
        if (enabled)
        {
            disabled.Remove(id);
            if (mod != null && !mod.Manifest.EnabledByDefault && !switchedOn.Contains(id))
                switchedOn.Add(id);
        }
        else
        {
            switchedOn.Remove(id);
            if (!disabled.Contains(id))
                disabled.Add(id);
        }
        _settings.Save();
        return null;
    }

    /// <summary>
    /// The essential mod that keeps <paramref name="mod"/> from being switched off or unloaded: the
    /// mod itself if its mod.json says "essential", or an essential mod that needs it. Null if none.
    /// </summary>
    public static ModInfo AlwaysOnBecause(ModInfo mod) =>
        mod.Manifest.Essential ? mod : EssentialDependent(mod);

    /// <summary>"it's always on", "X needs it and is always on", or null.</summary>
    public static string AlwaysOnReason(ModInfo mod)
    {
        var because = AlwaysOnBecause(mod);
        if (because == null)
            return null;
        return because == mod ? "it's always on" : $"{because.Manifest.DisplayName} needs it and is always on";
    }

    /// <summary>True if any on/off switch differs from what is running right now.</summary>
    public static bool RestartNeeded => All.Any(m => m.State != ModState.Failed && IsEnabled(m.Id) != (m.State == ModState.Loaded));

    /// <summary>An essential mod that depends on <paramref name="mod"/>, directly or further down.</summary>
    private static ModInfo EssentialDependent(ModInfo mod)
    {
        var seen = new HashSet<ModInfo> { mod };
        var queue = new Queue<ModInfo>();
        queue.Enqueue(mod);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var other in All)
            {
                if (seen.Contains(other) || !other.Manifest.Dependencies.ContainsKey(current.Id))
                    continue;
                if (other.Manifest.Essential)
                    return other;
                seen.Add(other);
                queue.Enqueue(other);
            }
        }
        return null;
    }
}
