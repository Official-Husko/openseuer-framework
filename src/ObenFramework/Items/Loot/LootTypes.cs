// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>Which of a loot category's two pools an entry is in. Containers draw from the rare pool by chance.</summary>
public enum LootTier
{
    Common = 0,
    Rare = 1,
}

/// <summary>A loot category (the game's <c>StorageSpawnCategory</c>, named by containers' <c>itemSpawnCategory</c>).</summary>
public readonly struct LootCategoryInfo
{
    public LootCategoryInfo(int id, string name, int commonEntries, int rareEntries, bool modded)
    {
        Id = id;
        Name = name;
        CommonEntries = commonEntries;
        RareEntries = rareEntries;
        Modded = modded;
    }

    public int Id { get; }

    public string Name { get; }

    /// <summary>Length of the common pool, every copy counted (an item listed three times weighs three).</summary>
    public int CommonEntries { get; }

    public int RareEntries { get; }

    /// <summary>Defined by a mod through <see cref="LootPools.DefineCategory"/>.</summary>
    public bool Modded { get; }
}

/// <summary>One item of a loot pool with its weight: how many times the pool lists it.</summary>
public readonly struct LootEntryInfo
{
    public LootEntryInfo(int itemId, LootTier tier, int weight)
    {
        ItemId = itemId;
        Tier = tier;
        Weight = weight;
    }

    public int ItemId { get; }

    public LootTier Tier { get; }

    public int Weight { get; }
}

/// <summary>A container spawned its loot, raised by <see cref="LootPools.ContainerFilled"/>.</summary>
public readonly struct ContainerFilled
{
    public ContainerFilled(ContainerInfo container, List<int> itemIds)
    {
        Container = container;
        ItemIds = itemIds;
    }

    public ContainerInfo Container { get; }

    /// <summary>The items that appeared, one id per slot that was filled.</summary>
    public List<int> ItemIds { get; }
}
