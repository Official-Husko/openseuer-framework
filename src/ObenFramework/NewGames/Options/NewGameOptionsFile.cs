// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.NewGames;

/// <summary>One stored option value.</summary>
public sealed class NewGameOptionEntry
{
    public string ModId;
    public string Key;
    public string Value;
}

/// <summary>
/// The option values a game was started with, kept in the save's mod folder by the framework
/// (ObenFramework.newgame.json). Entries of mods that are gone stay in the file untouched and are
/// ignored. Plain data for Newtonsoft.Json, no Unity types.
/// </summary>
public sealed class NewGameOptionsFile
{
    public List<NewGameOptionEntry> Options = new List<NewGameOptionEntry>();

    /// <summary>The difficulty preset ("modId.id") the game was started with, or empty.</summary>
    public string DifficultyPreset = "";

    /// <summary>The stored value, or null when the save has none.</summary>
    public string Get(string modId, string key)
    {
        if (Options == null)
            return null;
        foreach (var entry in Options)
        {
            if (entry != null && entry.ModId == modId && entry.Key == key)
                return entry.Value;
        }
        return null;
    }

    public void Set(string modId, string key, string value)
    {
        Options ??= new List<NewGameOptionEntry>();
        foreach (var entry in Options)
        {
            if (entry != null && entry.ModId == modId && entry.Key == key)
            {
                entry.Value = value;
                return;
            }
        }
        Options.Add(new NewGameOptionEntry { ModId = modId, Key = key, Value = value });
    }

    /// <summary>Copies the option values (not the preset) of <paramref name="other"/> into this file.</summary>
    public void CopyOptionsFrom(NewGameOptionsFile other)
    {
        Options = new List<NewGameOptionEntry>();
        if (other?.Options == null)
            return;
        foreach (var entry in other.Options)
        {
            if (entry != null && entry.ModId != null && entry.Key != null && entry.Value != null)
                Options.Add(new NewGameOptionEntry { ModId = entry.ModId, Key = entry.Key, Value = entry.Value });
        }
    }

    internal void Sort()
    {
        Options?.Sort((a, b) =>
        {
            int byMod = string.CompareOrdinal(a.ModId, b.ModId);
            return byMod != 0 ? byMod : string.CompareOrdinal(a.Key, b.Key);
        });
    }
}
