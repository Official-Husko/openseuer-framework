using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Assets;

/// <summary>The asset set of every mod that has loaded something, found by mod id.</summary>
internal static class AssetSets
{
    private static readonly Dictionary<string, ModAssetSet> Sets = new Dictionary<string, ModAssetSet>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the owner's set, creating it on first use. It is released when the owner unloads.</summary>
    public static ModAssetSet For(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        string id = owner.Info.Id;
        if (Sets.TryGetValue(id, out var existing))
        {
            if (existing.Owner == owner && !existing.Released)
                return existing;
            // A copy left behind by an earlier load of the same mod.
            Drop(existing);
        }

        var set = new ModAssetSet(owner);
        Sets[id] = set;
        owner.Track(Disposable.Create(() => Drop(set)));
        return set;
    }

    public static ModAssetSet Find(string modId) =>
        modId != null && Sets.TryGetValue(modId, out var set) ? set : null;

    public static List<ModAssetSet> All() => new List<ModAssetSet>(Sets.Values);

    public static void ReleaseAll()
    {
        foreach (var set in All())
            Drop(set);
    }

    private static void Drop(ModAssetSet set)
    {
        set.Release();
        if (Sets.TryGetValue(set.ModId, out var current) && current == set)
            Sets.Remove(set.ModId);
    }
}
