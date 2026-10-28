using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// The kinds of furniture the game knows, found by the id that placed furniture reports as its
/// <see cref="FurnitureInfo.TypeId"/> (an asset path such as
/// <c>Assets/Content/Prefabs/Building System/Furniture/Chairs/Red Metal Chair.asset</c>). Lookups also accept
/// the asset's short name (<c>Red Metal Chair</c>, what the developer console's <c>add_furniture</c> takes)
/// and, as a last resort, the title, ignoring case.
/// </summary>
public static class FurnitureKinds
{
    private static readonly Dictionary<string, Furniture> Index = new Dictionary<string, Furniture>(StringComparer.OrdinalIgnoreCase);
    private static int _indexedCount = -1;

    /// <summary>The furniture the developer console lists, which is every piece in the build menu. Don't modify the list.</summary>
    public static IReadOnlyList<Furniture> All
    {
        get
        {
            var console = DeveloperConsole.Instance;
            return console != null && console.furniture != null ? console.furniture.furnitures : (IReadOnlyList<Furniture>)Array.Empty<Furniture>();
        }
    }

    /// <summary>
    /// The furniture for an id, short name or title, or null. An id that is an asset path and isn't in the
    /// console's list (a piece the game only references from a scene) is loaded from the game's assets.
    /// </summary>
    public static Furniture Find(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return null;

        kind = kind.Trim();
        Refresh();
        if (Index.TryGetValue(kind, out var known) && known != null)
            return known;

        // Pieces standing in a zone that the list lacks still tell their id; match them first.
        foreach (var placed in PlacedFurniture.All(includePreplaced: true))
        {
            if (placed.furniture != null && string.Equals(placed.furniture.Id, kind, StringComparison.OrdinalIgnoreCase))
                return Remember(placed.furniture);
        }

        if (kind.IndexOf('/') >= 0 && kind.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            return Remember(SavableScriptableObject.LoadFromPath(kind) as Furniture);
        return null;
    }

    /// <summary>Furniture whose id, name or title contains the text, ignoring case; exact name matches first.</summary>
    public static List<Furniture> Search(string text, int max = 50)
    {
        var exact = new List<Furniture>();
        var partial = new List<Furniture>();
        if (string.IsNullOrWhiteSpace(text) || max <= 0)
            return exact;

        text = text.Trim();
        foreach (var furniture in All)
        {
            if (furniture == null)
                continue;
            if (string.Equals(furniture.name, text, StringComparison.OrdinalIgnoreCase) || string.Equals(furniture.Id, text, StringComparison.OrdinalIgnoreCase))
                exact.Add(furniture);
            else if (Contains(furniture.Id, text) || Contains(furniture.title, text))
                partial.Add(furniture);
        }

        exact.AddRange(partial);
        if (exact.Count > max)
            exact.RemoveRange(max, exact.Count - max);
        return exact;
    }

    internal static void Reset()
    {
        Index.Clear();
        _indexedCount = -1;
    }

    private static bool Contains(string haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static Furniture Remember(Furniture furniture)
    {
        if (furniture != null)
            Index[furniture.Id] = furniture;
        return furniture;
    }

    private static void AddIfNew(string key, Furniture furniture)
    {
        if (!string.IsNullOrEmpty(key) && !Index.ContainsKey(key))
            Index[key] = furniture;
    }

    // The list is a scene object that is rebuilt with the scene; rebuild the index when it changes.
    private static void Refresh()
    {
        var all = All;
        if (all.Count == _indexedCount && Index.Count > 0)
            return;

        Index.Clear();
        _indexedCount = all.Count;
        foreach (var furniture in all)
        {
            if (furniture != null)
                AddIfNew(furniture.Id, furniture);
        }
        foreach (var furniture in all)
        {
            if (furniture != null)
                AddIfNew(furniture.name, furniture);
        }
        foreach (var furniture in all)
        {
            if (furniture != null)
                AddIfNew(furniture.title, furniture);
        }
    }
}
