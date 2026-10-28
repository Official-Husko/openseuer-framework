// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.World;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Building;

/// <summary>One defined piece: its definition, the furniture built from it and what hangs off it.</summary>
internal sealed class FurnitureEntry
{
    public FurnitureEntry(string id, string modId, FurnitureDefinition definition)
    {
        Id = id;
        ModId = modId;
        Definition = definition;
    }

    public string Id { get; }
    public string ModId { get; }
    public FurnitureDefinition Definition { get; }

    /// <summary>The game furniture built from the definition, once something asked for it.</summary>
    public global::Furniture Built;

    /// <summary>The build failed once; not tried again until the next scene.</summary>
    public bool BuildFailed;

    public BlueprintEntry Blueprint;

    public bool SoldAt(Economy.ShopInfo shop)
    {
        foreach (string key in Definition.Shops)
        {
            if (shop != null && shop.Matches(key))
                return true;
        }
        return false;
    }
}

/// <summary>
/// The defined pieces by id, and the builds of pieces whose mod unloaded during the session (placed copies
/// still point at them, so they stay alive until the game quits).
/// </summary>
internal static class FurnitureRegistry
{
    private static readonly Dictionary<string, FurnitureEntry> Entries = new Dictionary<string, FurnitureEntry>(StringComparer.Ordinal);
    private static readonly Dictionary<string, global::Furniture> Retired = new Dictionary<string, global::Furniture>(StringComparer.Ordinal);

    internal static readonly OnceLog Log = new OnceLog("Custom furniture");

    public static ICollection<FurnitureEntry> All => Entries.Values;

    public static FurnitureEntry Find(string id) => id != null && Entries.TryGetValue(id, out var entry) ? entry : null;

    public static void Add(ITracker owner, FurnitureEntry entry)
    {
        Entries.Add(entry.Id, entry);
        owner.Track(Disposable.Create(() => Remove(entry)));
    }

    /// <summary>The furniture for an id: the built piece, else one retired this session, else null.</summary>
    public static global::Furniture Resolve(string id)
    {
        var entry = Find(id);
        if (entry != null)
        {
            var built = FurnitureBuild.Registered(entry);
            if (built != null)
                return built;
        }
        return id != null && Retired.TryGetValue(id, out var old) && old != null ? old : null;
    }

    public static void ForgetFailures()
    {
        foreach (var entry in Entries.Values)
            entry.BuildFailed = false;
    }

    private static void Remove(FurnitureEntry entry)
    {
        if (!Entries.TryGetValue(entry.Id, out var current) || current != entry)
            return;
        Entries.Remove(entry.Id);
        FurnitureBlueprints.Withdraw(entry);
        if (entry.Built != null)
            Retired[entry.Id] = entry.Built;
    }

    /// <summary>Called when the framework unloads: forget the definitions, keep the built objects alive.</summary>
    public static void Reset()
    {
        foreach (var entry in new List<FurnitureEntry>(Entries.Values))
            Remove(entry);
        Entries.Clear();
        Retired.Clear();
        Log.Reset();
    }
}
