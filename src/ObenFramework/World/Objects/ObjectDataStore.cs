// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.World;

/// <summary>One mod's text attached to one saved scene object.</summary>
public sealed class ObjectDataEntry
{
    public string Mod;
    public string Scene;
    public string Guid;
    public string Blob;
}

/// <summary>The saved form: every entry of every mod.</summary>
public sealed class ObjectDataFile
{
    public int Version = 1;
    public List<ObjectDataEntry> Entries = new List<ObjectDataEntry>();
}

/// <summary>
/// Text mods attach to the game's saved objects (by scene and GUID), with a size budget per mod so one
/// mod can't bloat every save. No Unity types, covered by unit tests.
/// </summary>
public sealed class ObjectDataStore
{
    public const int MaxBlobLength = 16384;
    public const int MaxPerMod = 262144;

    // What an entry costs besides its text: keys and JSON overhead, so many tiny entries count too.
    public const int EntryOverhead = 128;

    private readonly Dictionary<string, ObjectDataEntry> _entries = new Dictionary<string, ObjectDataEntry>();
    private readonly Dictionary<string, int> _used = new Dictionary<string, int>();

    public int Count => _entries.Count;

    public static int Cost(string blob) => (blob != null ? blob.Length : 0) + EntryOverhead;

    /// <summary>How much of its budget a mod uses.</summary>
    public int Used(string mod) => _used.TryGetValue(mod, out int used) ? used : 0;

    public string Get(string mod, string scene, string guid) =>
        _entries.TryGetValue(Key(mod, scene, guid), out var entry) ? entry.Blob : null;

    /// <summary>Stores or replaces a blob. False when it is too long or the mod's budget would run over.</summary>
    public bool Put(string mod, string scene, string guid, string blob)
    {
        if (string.IsNullOrEmpty(mod) || string.IsNullOrEmpty(guid) || blob == null || blob.Length > MaxBlobLength)
            return false;
        string key = Key(mod, scene, guid);
        int oldCost = _entries.TryGetValue(key, out var old) ? Cost(old.Blob) : 0;
        int next = Used(mod) - oldCost + Cost(blob);
        if (next > MaxPerMod)
            return false;
        _entries[key] = new ObjectDataEntry { Mod = mod, Scene = scene ?? "", Guid = guid, Blob = blob };
        _used[mod] = next;
        return true;
    }

    /// <summary>Removes a blob. Returns whether there was one.</summary>
    public bool Remove(string mod, string scene, string guid)
    {
        string key = Key(mod, scene, guid);
        if (!_entries.TryGetValue(key, out var old))
            return false;
        _entries.Remove(key);
        _used[mod] = Used(mod) - Cost(old.Blob);
        return true;
    }

    public void Clear()
    {
        _entries.Clear();
        _used.Clear();
    }

    /// <summary>Replaces everything with a saved file's entries, dropping those that break the limits.</summary>
    public void Load(ObjectDataFile file)
    {
        Clear();
        if (file == null || file.Entries == null)
            return;
        foreach (var entry in file.Entries)
        {
            if (entry != null)
                Put(entry.Mod, entry.Scene, entry.Guid, entry.Blob);
        }
    }

    /// <summary>Writes the entries into <paramref name="file"/> for saving.</summary>
    public void SaveTo(ObjectDataFile file)
    {
        file.Entries = new List<ObjectDataEntry>(_entries.Values);
    }

    private static string Key(string mod, string scene, string guid) => mod + "\n" + (scene ?? "") + "\n" + guid;
}
