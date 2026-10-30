// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenLoader.Runtime;
using OS.Items;
using UnityEngine;

namespace ObenFramework.Building;

/// <summary>
/// Custom furniture as loot: a mod adds its piece to a loot category with a weight, the framework puts the
/// game's flatpack item into that category's pool (with the summed weight of every piece in it) and fills each
/// flatpack the category spawns with one of the pieces, picked by weight. Covers containers, ground spawners
/// and packages rolled from the category, as well as flatpacks a package of the category holds.
/// </summary>
internal static class FurnitureLoot
{
    private sealed class Record
    {
        public string FurnitureId;
        public int Weight;
    }

    private static readonly Dictionary<string, List<Record>> Categories = new Dictionary<string, List<Record>>(StringComparer.Ordinal);
    private static readonly Dictionary<string, IDisposable> PoolEntries = new Dictionary<string, IDisposable>(StringComparer.Ordinal);
    private static readonly List<int> Weights = new List<int>();

    public static IDisposable Add(ITracker owner, string furnitureId, string category, int weight)
    {
        if (!Categories.TryGetValue(category, out var records))
            Categories[category] = records = new List<Record>();
        var record = new Record { FurnitureId = furnitureId, Weight = weight };
        records.Add(record);
        Rebuild(category);
        var registration = Disposable.Create(() =>
        {
            if (Categories.TryGetValue(category, out var list) && list.Remove(record))
                Rebuild(category);
        });
        owner.Track(registration);
        return registration;
    }

    public static void Reset()
    {
        foreach (var entry in PoolEntries.Values)
            entry.Dispose();
        PoolEntries.Clear();
        Categories.Clear();
    }

    // The flatpack's weight in the category is the pieces' weights added up (capped at the pool's maximum).
    private static void Rebuild(string category)
    {
        if (PoolEntries.TryGetValue(category, out var old))
        {
            old.Dispose();
            PoolEntries.Remove(category);
        }
        var records = Categories[category];
        if (records.Count == 0)
            return;
        long total = 0;
        foreach (var record in records)
            total += record.Weight;
        var tracker = Building.CustomFurniture.Owner;
        if (tracker != null)
            PoolEntries[category] = LootPools.Add(tracker, category, FurnitureFlatpacks.ItemId, (int)Math.Min(FurnitureIds.MaxLootWeight, total));
    }

    /// <summary>A piece for a flatpack rolled from a category, or null when the category has none of ours.</summary>
    public static global::Furniture Pick(string category)
    {
        if (category == null || !Categories.TryGetValue(category, out var records) || records.Count == 0)
            return null;
        Weights.Clear();
        foreach (var record in records)
            Weights.Add(record.Weight);
        int index = FurnitureIds.PickWeighted(Weights, UnityEngine.Random.Range(0, (int)FurnitureIds.TotalWeight(Weights)));
        return index >= 0 ? FurnitureRegistry.Resolve(records[index].FurnitureId) : null;
    }

    private static bool Holds(string category) => category != null && Categories.TryGetValue(category, out var records) && records.Count > 0;

    // False when the flatpack has to go: the category holds mod pieces but none of them built. An empty
    // flatpack from a category without mod pieces is the game's own business.
    private static bool Fill(string category, object[] meta)
    {
        if (!FurnitureFlatpacks.IsEmpty(meta) || !Holds(category))
            return true;
        var piece = Pick(category);
        if (piece == null)
            return false;
        FurnitureFlatpacks.Fill(meta, piece);
        return true;
    }

    [HarmonyPatch(typeof(Storage), nameof(Storage.SetNextItemsToSpawn))]
    private static class ContainerPatch
    {
        [HarmonyPostfix]
        private static void After(Storage __instance, List<ItemStackSpawn> ___nextItemsToSpawn, bool ___spawnItemsFromCategory)
        {
            if (!___spawnItemsFromCategory || ___nextItemsToSpawn == null || Categories.Count == 0)
                return;
            try
            {
                for (int i = ___nextItemsToSpawn.Count - 1; i >= 0; i--)
                {
                    var spawn = ___nextItemsToSpawn[i];
                    int itemId = spawn.itemId == 0 && spawn.itemReference != null ? spawn.itemReference.ID : spawn.itemId;
                    if (itemId == FurnitureFlatpacks.ItemId && !Fill(__instance.itemSpawnCategory, spawn.Meta))
                        ___nextItemsToSpawn.RemoveAt(i);
                }
            }
            catch (Exception e) { FurnitureRegistry.Log.Error("Container loot hook", e); }
        }
    }

    [HarmonyPatch(typeof(CollectibleItemSpawner), "SpawnItem")]
    private static class GroundPatch
    {
        [HarmonyPrefix]
        private static bool Before(CollectibleItemSpawner __instance, Item item, StorageSpawnCategory category, ref object[] savedMeta)
        {
            if (item == null || item.ID != FurnitureFlatpacks.ItemId || savedMeta != null || category == null || !Holds(category.Name))
                return true;
            try
            {
                var piece = Pick(category.Name);
                if (piece == null)
                    return false;
                // The item data the spawner would make itself (its owner, its spawn values), holding the piece.
                var values = __instance.useSpawnInfo ? __instance.itemSpawnInfo : category.MetaValues;
                int owner = __instance.owner != null ? __instance.owner.ID : 0;
                savedMeta = FurnitureFlatpacks.Fill(ItemMetaUtilities.GetMetaForItem(new ItemReference(item), values, owner), piece);
            }
            catch (Exception e) { FurnitureRegistry.Log.Error("Ground loot hook", e); }
            return true;
        }
    }

    [HarmonyPatch(typeof(StorageSpawnCategory), nameof(StorageSpawnCategory.GetRandomItemWithMeta))]
    private static class PackagePatch
    {
        [HarmonyPostfix]
        private static void After(StorageSpawnCategory __instance, ref (Item, object[]) __result)
        {
            if (Categories.Count == 0 || __result.Item1 == null || __result.Item1.ID != FurnitureFlatpacks.ItemId)
                return;
            try { Fill(__instance.Name, __result.Item2); }
            catch (Exception e) { FurnitureRegistry.Log.Error("Package loot hook", e); }
        }
    }

    // A package rolled from a category without the category's item data gets fresh data for each item it
    // holds, so a flatpack in it comes out empty; fill those from the package's category.
    [HarmonyPatch(typeof(ItemMetaUtilities), nameof(ItemMetaUtilities.GetMetaForItem))]
    private static class PackageContentsPatch
    {
        [HarmonyPostfix]
        private static void After(StorageMetaValuesBase metaValues, object[] __result)
        {
            if (Categories.Count == 0 || __result == null || !(metaValues is StorageMetaValues values) || values.package == null || !values.package.spawnFromCategory)
                return;
            try
            {
                var contents = Item.GetMetaOfType<ItemPackageData>(__result)?.containedItems;
                if (contents == null)
                    return;
                for (int i = contents.Count - 1; i >= 0; i--)
                {
                    var child = contents[i];
                    if (child != null && child.item != null && child.item.ID == FurnitureFlatpacks.ItemId && !Fill(values.package.spawnCategoroyName, child.meta))
                        contents.RemoveAt(i);
                }
            }
            catch (Exception e) { FurnitureRegistry.Log.Error("Package contents loot hook", e); }
        }
    }
}
