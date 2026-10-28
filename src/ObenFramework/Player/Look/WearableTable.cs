// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Player.Look;

/// <summary>
/// Every wearable the game ships, by Resources folder and asset name, loaded once. The game itself calls
/// <c>Resources.LoadAll</c> 14 times for every Appearance it creates, so dressing several puppets through
/// this table costs one load per folder in total. Wearables are shared assets: they are only ever read.
/// </summary>
internal static class WearableTable
{
    private const string NoneName = "-";

    private static readonly Dictionary<string, Dictionary<string, Wearable>> Folders =
        new Dictionary<string, Dictionary<string, Wearable>>();

    /// <summary>The item called <paramref name="name"/> in the slot's folder, or the slot's "none" item when the name is empty or unknown.</summary>
    public static Wearable Find(LookSlot slot, bool male, string name)
    {
        var items = Items(slot.Folder(male));
        if (!string.IsNullOrEmpty(name) && items.TryGetValue(name, out var found) && found != null)
            return found;
        return items.TryGetValue(NoneName, out var none) ? none : null;
    }

    /// <summary>The first item of a slot's folder, for slots that must wear something (heads).</summary>
    public static Wearable First(LookSlot slot, bool male)
    {
        foreach (var wearable in Items(slot.Folder(male)).Values)
            return wearable;
        return null;
    }

    /// <summary>True when the slot's folder has an item with this exact name.</summary>
    public static bool Has(LookSlot slot, bool male, string name) =>
        !string.IsNullOrEmpty(name) && Items(slot.Folder(male)).ContainsKey(name);

    /// <summary>The names of everything a slot can wear, "-" (nothing) included.</summary>
    public static IReadOnlyCollection<string> Names(LookSlot slot, bool male) => Items(slot.Folder(male)).Keys;

    public static void Clear() => Folders.Clear();

    private static Dictionary<string, Wearable> Items(string folder)
    {
        if (Folders.TryGetValue(folder, out var items) && IsAlive(items))
            return items;

        items = new Dictionary<string, Wearable>();
        foreach (var wearable in Resources.LoadAll<Wearable>(folder))
        {
            if (wearable != null)
                items[wearable.name] = wearable;
        }
        Folders[folder] = items;
        return items;
    }

    // Resources.UnloadUnusedAssets can drop a folder's assets; a dead entry means reload that folder.
    private static bool IsAlive(Dictionary<string, Wearable> items)
    {
        foreach (var wearable in items.Values)
            return wearable != null;
        return false;
    }
}
