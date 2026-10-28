// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;
using ObenLoader.Runtime;

namespace ObenFramework.Items;

/// <summary>
/// The loot tables containers fill themselves from (the game's storage spawn categories, from
/// StorageSpawnCategory.json). Mods can add items to a category's common or rare pool with a weight, define
/// new categories for their own containers, and hear when a container spawns its loot. Additions are kept in
/// memory only: they are put back whenever the game rebuilds its table, wait for mod items that don't exist yet,
/// and are taken out again when their mod unloads. The game's file is never written.
/// </summary>
public static class LootPools
{
    private const int FirstModCategoryId = 1_000_000;

    private static readonly AccessTools.FieldRef<StorageSpawnCategoryDatabase, List<StorageSpawnCategory>> Database =
        AccessTools.FieldRefAccess<StorageSpawnCategoryDatabase, List<StorageSpawnCategory>>("database");

    private static readonly List<Record> Records = new List<Record>();
    private static readonly List<CategoryRecord> Defined = new List<CategoryRecord>();
    private static int _nextCategoryId = FirstModCategoryId;

    /// <summary>
    /// A container spawned loot while the world is up, which is when it respawns on the game's clock. The fills
    /// a scene does while it loads are not reported.
    /// </summary>
    public static readonly WorldEvent<ContainerFilled> ContainerFilled = new WorldEvent<ContainerFilled>(nameof(ContainerFilled));

    /// <summary>Every category the game knows now, mod ones included.</summary>
    public static List<LootCategoryInfo> Categories()
    {
        var list = new List<LootCategoryInfo>();
        var database = StorageSpawnCategoryDatabase.instance;
        if (database == null)
            return list;
        foreach (int id in database.ListStorageSpawnCategoryByID())
        {
            var category = database.FetchStorageSpawnCategoryByID(id);
            if (category != null)
                list.Add(new LootCategoryInfo(category.ID, category.Name, Size(category.ItemCommon), Size(category.ItemRare), category.ID >= FirstModCategoryId));
        }
        return list;
    }

    /// <summary>The items of a category with their weights, common pool first. Empty when it doesn't exist.</summary>
    public static List<LootEntryInfo> PoolOf(string category)
    {
        var list = new List<LootEntryInfo>();
        var live = Live(category);
        if (live == null)
            return list;
        foreach (var pair in LootWeights.Count(live.ItemCommon ?? new List<int>()))
            list.Add(new LootEntryInfo(pair.Key, LootTier.Common, pair.Value));
        foreach (var pair in LootWeights.Count(live.ItemRare ?? new List<int>()))
            list.Add(new LootEntryInfo(pair.Key, LootTier.Rare, pair.Value));
        return list;
    }

    /// <summary>
    /// Adds an item to a category's pool until <paramref name="owner"/> unloads or the returned object is
    /// disposed. The weight (1 to 100) is how many times it is listed; a vanilla entry usually has 1 to 5.
    /// Several mods adding the same item add up.
    /// </summary>
    public static IDisposable Add(ITracker owner, string category, int itemId, int weight = 1, LootTier tier = LootTier.Common)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(category))
            throw new ArgumentException("No category given.", nameof(category));
        if (weight < 1 || weight > LootWeights.MaxWeight)
            throw new ArgumentOutOfRangeException(nameof(weight), $"The weight must be 1 to {LootWeights.MaxWeight}.");
        if (PlayerItems.IsMoney(itemId))
            throw new ArgumentException("Money ids can't be loot.", nameof(itemId));

        var record = new Record { Category = category, ItemId = itemId, Weight = weight, Tier = tier };
        Records.Add(record);
        Apply(record);
        var registration = Disposable.Create(() =>
        {
            Undo(record);
            Records.Remove(record);
        });
        owner.Track(registration);
        return registration;
    }

    /// <summary>
    /// Defines a new, empty category for a mod's own containers (set their <c>itemSpawnCategory</c> to the
    /// name), until <paramref name="owner"/> unloads. Throws when a game or mod category already has the name.
    /// </summary>
    public static IDisposable DefineCategory(ITracker owner, string name)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(name) || name == "None")
            throw new ArgumentException("A category needs a name other than None.", nameof(name));
        if (Defined.Exists(c => c.Name == name) || (Live(name) != null))
            throw new ArgumentException($"A loot category named '{name}' already exists.", nameof(name));

        var record = new CategoryRecord { Name = name, Id = _nextCategoryId++ };
        Defined.Add(record);
        ApplyCategory(record);
        Sync();
        var registration = Disposable.Create(() =>
        {
            Defined.Remove(record);
            var list = StorageSpawnCategoryDatabase.instance != null ? Database(StorageSpawnCategoryDatabase.instance) : null;
            list?.RemoveAll(c => c != null && c.ID == record.Id);
        });
        owner.Track(registration);
        return registration;
    }

    /// <summary>Puts every mod category and entry into the current table. Cheap when nothing changed.</summary>
    internal static void Sync()
    {
        foreach (var category in Defined)
            ApplyCategory(category);
        foreach (var record in Records)
            Apply(record);
    }

    internal static StorageSpawnCategory Live(string category)
    {
        var database = StorageSpawnCategoryDatabase.instance;
        return database != null && !string.IsNullOrEmpty(category) ? database.FetchStorageSpawnCategory(category) : null;
    }

    private static void ApplyCategory(CategoryRecord record)
    {
        var database = StorageSpawnCategoryDatabase.instance;
        var list = database != null ? Database(database) : null;
        if (list == null || list.Exists(c => c != null && (c.ID == record.Id || c.Name == record.Name)))
            return;
        list.Add(new StorageSpawnCategory { ID = record.Id, Name = record.Name, ItemCommon = new List<int>(), ItemRare = new List<int>() });
    }

    private static void Apply(Record record)
    {
        var category = Live(record.Category);
        if (category != null && ReferenceEquals(record.Target, category))
            return;
        if (category == null || !ItemLookup.Exists(record.ItemId))
        {
            if (!record.Warned && StorageSpawnCategoryDatabase.instance != null && ItemLookup.IsLoaded)
            {
                record.Warned = true;
                ItemsLog.Warn($"A loot entry waits: category '{record.Category}' or item {record.ItemId} doesn't exist yet.");
            }
            return;
        }
        var pool = Pool(category, record.Tier);
        LootWeights.Add(pool, record.ItemId, record.Weight);
        record.Target = category;
    }

    private static void Undo(Record record)
    {
        var category = Live(record.Category);
        if (category != null && ReferenceEquals(record.Target, category))
            LootWeights.Remove(Pool(category, record.Tier), record.ItemId, record.Weight);
        record.Target = null;
    }

    private static List<int> Pool(StorageSpawnCategory category, LootTier tier)
    {
        if (tier == LootTier.Rare)
            return category.ItemRare ??= new List<int>();
        return category.ItemCommon ??= new List<int>();
    }

    private static int Size(List<int> pool) => pool != null ? pool.Count : 0;

    private sealed class Record
    {
        public string Category;
        public int ItemId;
        public int Weight;
        public LootTier Tier;
        public StorageSpawnCategory Target;
        public bool Warned;
    }

    private sealed class CategoryRecord
    {
        public string Name;
        public int Id;
    }
}
