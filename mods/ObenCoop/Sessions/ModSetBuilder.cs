using System.Collections.Generic;
using ObenCoop.Net.Session;
using ObenLoader.Mods;

namespace ObenCoop.Sessions;

/// <summary>Works out which loaded mods have to match between host and clients.</summary>
internal static class ModSetBuilder
{
    /// <summary>Every loaded mod except the ones the settings leave out.</summary>
    public static ModSet Current(ICollection<string> ignored)
    {
        var entries = new List<ModEntry>();
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State != ModState.Loaded || (ignored != null && ignored.Contains(mod.Id)))
                continue;
            entries.Add(new ModEntry(mod.Id, mod.Manifest.Version));
        }
        return new ModSet(entries);
    }
}
