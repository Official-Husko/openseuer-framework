// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>One mod item a save knows about: its id, the mod that adds it and its title.</summary>
public sealed class ItemManifestEntry
{
    public int Id;
    public string Mod;
    public string Title;
}

/// <summary>What the framework keeps in each save about mod items (<c>mods/ObenFramework.items.json</c>).</summary>
public sealed class ItemManifestFile
{
    public List<ItemManifestEntry> Items = new List<ItemManifestEntry>();
}

/// <summary>
/// The mod items a save knows about. Every save writes the items mods register now, plus the ones the
/// loaded save listed whose mod isn't adding them any more ("carried"), so a missing item keeps its mod's
/// name and title from save to save until the mod comes back. Plain bookkeeping, tested on its own.
/// </summary>
internal sealed class ItemManifestBook
{
    public const int MaxCarried = 4096;

    private readonly Dictionary<int, ItemManifestEntry> _carried = new Dictionary<int, ItemManifestEntry>();

    public int CarriedCount => _carried.Count;

    /// <summary>The entries of a loaded save whose items aren't registered now. Returns how many were dropped over the cap.</summary>
    public int Load(ItemManifestFile file, Func<int, bool> isRegistered)
    {
        _carried.Clear();
        if (file?.Items == null)
            return 0;
        int dropped = 0;
        foreach (var entry in file.Items)
        {
            if (entry == null || !ReservedIds.Contains(entry.Id) || isRegistered(entry.Id) || _carried.ContainsKey(entry.Id))
                continue;
            if (_carried.Count >= MaxCarried)
            {
                dropped++;
                continue;
            }
            _carried[entry.Id] = Copy(entry);
        }
        return dropped;
    }

    /// <summary>The file to save: the registered items first, then carried ones that aren't registered again.</summary>
    public ItemManifestFile Build(IEnumerable<ItemManifestEntry> registered)
    {
        var file = new ItemManifestFile();
        var seen = new HashSet<int>();
        foreach (var entry in registered)
        {
            if (entry != null && seen.Add(entry.Id))
                file.Items.Add(Copy(entry));
        }
        foreach (var entry in _carried.Values)
        {
            if (seen.Add(entry.Id))
                file.Items.Add(Copy(entry));
        }
        return file;
    }

    /// <summary>What the save said about a missing item, or null.</summary>
    public ItemManifestEntry Find(int id) => _carried.TryGetValue(id, out var entry) ? entry : null;

    public IEnumerable<ItemManifestEntry> Carried => _carried.Values;

    /// <summary>Keeps an item that left while the game runs (its mod unloaded). False when it was already carried or the list is full.</summary>
    public bool Carry(ItemManifestEntry entry)
    {
        if (entry == null || !ReservedIds.Contains(entry.Id) || _carried.ContainsKey(entry.Id) || _carried.Count >= MaxCarried)
            return false;
        _carried[entry.Id] = Copy(entry);
        return true;
    }

    /// <summary>The item is registered again; it no longer needs carrying.</summary>
    public void Forget(int id) => _carried.Remove(id);

    public void Clear() => _carried.Clear();

    /// <summary>"Missing item (Mod: Title)" for a placeholder's name.</summary>
    public static string Label(ItemManifestEntry entry)
    {
        string mod = entry?.Mod ?? "";
        string title = entry?.Title ?? "";
        string detail = mod.Length == 0 ? title : title.Length == 0 ? mod : mod + ": " + title;
        return detail.Length == 0 ? "Missing item" : "Missing item (" + detail + ")";
    }

    private static ItemManifestEntry Copy(ItemManifestEntry entry) =>
        new ItemManifestEntry { Id = entry.Id, Mod = entry.Mod, Title = entry.Title };
}
