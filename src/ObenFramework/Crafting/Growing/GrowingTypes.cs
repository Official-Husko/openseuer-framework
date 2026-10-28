// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>One planting spot of a grow box or planter.</summary>
public readonly struct GrowingSpotInfo
{
    public GrowingSpotInfo(string spotId, string plantId, bool pickable)
    {
        SpotId = spotId;
        PlantId = plantId;
        Pickable = pickable;
    }

    public string SpotId { get; }

    /// <summary>The growing plant's id (see <see cref="Plants"/>), or null when the spot is empty or holds a pickable.</summary>
    public string PlantId { get; }

    /// <summary>The spot holds something to pick up as it is (a finished crop that doesn't grow).</summary>
    public bool Pickable { get; }
}

/// <summary>A snapshot of a growing station (the game's <c>Growing</c>: grow boxes, planters, mushroom beds).</summary>
public readonly struct GrowingStationInfo
{
    public GrowingStationInfo(string id, Vector3 position, int spots, bool paused, bool manualHarvest, List<GrowingSpotInfo> planted)
    {
        Id = id;
        Position = position;
        Spots = spots;
        Paused = paused;
        ManualHarvest = manualHarvest;
        Planted = planted;
    }

    /// <summary>The station id, usable with <see cref="CraftingStations"/> too.</summary>
    public string Id { get; }

    public Vector3 Position { get; }

    public int Spots { get; }

    public bool Paused { get; }

    /// <summary>Crops are picked by hand rather than with the window's harvest button.</summary>
    public bool ManualHarvest { get; }

    /// <summary>The spots that hold something.</summary>
    public List<GrowingSpotInfo> Planted { get; }
}

/// <summary>A snapshot of a growing plant.</summary>
public readonly struct PlantInfo
{
    public PlantInfo(string id, Vector3 position, int age, int fullGrownAge, float stageProgress, bool paused, bool rotting, int readyFruit)
    {
        Id = id;
        Position = position;
        Age = age;
        FullGrownAge = fullGrownAge;
        StageProgress = stageProgress;
        Paused = paused;
        Rotting = rotting;
        ReadyFruit = readyFruit;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>Age in game seconds.</summary>
    public int Age { get; }

    public int FullGrownAge { get; }

    public bool FullyGrown => Age >= FullGrownAge;

    /// <summary>Progress through the current growth stage, 0 to 1.</summary>
    public float StageProgress { get; }

    public bool Paused { get; }

    public bool Rotting { get; }

    /// <summary>Fruit ready to pick now.</summary>
    public int ReadyFruit { get; }
}

/// <summary>One fruit (or other pickable part) of a plant.</summary>
public readonly struct FruitInfo
{
    public FruitInfo(int itemId, int amount, bool visible, bool ready, bool spoiled, float growProgress, float spoilProgress)
    {
        ItemId = itemId;
        Amount = amount;
        Visible = visible;
        Ready = ready;
        Spoiled = spoiled;
        GrowProgress = growProgress;
        SpoilProgress = spoilProgress;
    }

    public int ItemId { get; }

    public int Amount { get; }

    public bool Visible { get; }

    public bool Ready { get; }

    public bool Spoiled { get; }

    public float GrowProgress { get; }

    public float SpoilProgress { get; }
}

/// <summary>A plant was harvested whole, raised by <see cref="Plants.Harvested"/>.</summary>
public readonly struct PlantHarvest
{
    public PlantHarvest(string plantId, List<ItemCount> items)
    {
        PlantId = plantId;
        Items = items;
    }

    public string PlantId { get; }

    public List<ItemCount> Items { get; }
}

/// <summary>A fruit was picked from a plant, raised by <see cref="Plants.Picked"/>.</summary>
public readonly struct PlantPick
{
    public PlantPick(string plantId, int itemId, int amount)
    {
        PlantId = plantId;
        ItemId = itemId;
        Amount = amount;
    }

    public string PlantId { get; }

    public int ItemId { get; }

    public int Amount { get; }
}
