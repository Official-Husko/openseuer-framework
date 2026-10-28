// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Npcs;

/// <summary>One mod's choice to tame or untame one animal, identified by its scene and save id.</summary>
[Serializable]
internal sealed class AnimalTameEntry
{
    public string Mod;
    public string Scene;
    public string Guid;
    public bool Tame;
}

/// <summary>
/// Tame choices mods made, kept per save. The game saves an animal's tame flag inside its item data but
/// does not put it back on the animal of a scene when the level loads, so the framework re-applies these
/// after every load. No Unity types, so it is unit tested.
/// </summary>
[Serializable]
internal sealed class AnimalTameFile
{
    public const int MaxPerMod = 1024;
    public const int MaxTotal = 16384;

    public List<AnimalTameEntry> Entries = new List<AnimalTameEntry>();

    /// <summary>Records a choice, replacing the same mod's earlier one for that animal. False when the file is full.</summary>
    public bool Set(string mod, string scene, string guid, bool tame)
    {
        foreach (var entry in Entries)
        {
            if (entry.Mod == mod && entry.Scene == scene && entry.Guid == guid)
            {
                entry.Tame = tame;
                return true;
            }
        }
        if (Count(mod) >= MaxPerMod || Entries.Count >= MaxTotal)
            return false;
        Entries.Add(new AnimalTameEntry { Mod = mod, Scene = scene, Guid = guid, Tame = tame });
        return true;
    }

    public int Count(string mod)
    {
        int count = 0;
        foreach (var entry in Entries)
        {
            if (entry.Mod == mod)
                count++;
        }
        return count;
    }

    /// <summary>Forgets every choice of the mod. Returns how many there were.</summary>
    public int Forget(string mod) => Entries.RemoveAll(e => e.Mod == mod);

    /// <summary>
    /// The choice that applies to one animal: the last one recorded by a mod <paramref name="loaded"/> says
    /// is there, or null when there is none.
    /// </summary>
    public bool? For(string scene, string guid, Func<string, bool> loaded)
    {
        bool? result = null;
        foreach (var entry in Entries)
        {
            if (entry.Scene == scene && entry.Guid == guid && loaded(entry.Mod))
                result = entry.Tame;
        }
        return result;
    }

    /// <summary>A copy without broken entries and repeats, within the limits.</summary>
    public static AnimalTameFile Clean(AnimalTameFile file)
    {
        var clean = new AnimalTameFile();
        if (file == null || file.Entries == null)
            return clean;
        foreach (var entry in file.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Mod) || string.IsNullOrEmpty(entry.Scene) || string.IsNullOrEmpty(entry.Guid))
                continue;
            clean.Set(entry.Mod, entry.Scene, entry.Guid, entry.Tame);
        }
        return clean;
    }
}
