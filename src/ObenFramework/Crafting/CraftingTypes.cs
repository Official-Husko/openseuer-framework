// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>An item id with an amount, as recipes list them (amounts may be fractions for liquids).</summary>
public readonly struct ItemCount
{
    public ItemCount(int itemId, float amount)
    {
        ItemId = itemId;
        Amount = amount;
    }

    public int ItemId { get; }

    public float Amount { get; }

    public override string ToString() => $"{Amount} x {ItemId}";

    internal static List<ItemCount> From(Dictionary<ItemReference, float> items, float multiplier = 1f)
    {
        var list = new List<ItemCount>();
        if (items == null)
            return list;
        foreach (var entry in items)
        {
            if (entry.Key != null)
                list.Add(new ItemCount(entry.Key.ID, entry.Value * multiplier));
        }
        return list;
    }
}

/// <summary>A snapshot of one crafting station (a stove, workbench, still, grow box, dishwasher).</summary>
public sealed class StationInfo
{
    internal StationInfo(CraftingBase station, string id, List<string> recipeTypes, List<string> modifiers)
    {
        Station = station;
        Id = id;
        Kind = station.GetType().Name;
        Name = station.ManuName;
        Position = station.transform.position;
        Scene = station.gameObject.scene.name;
        Busy = station.CraftingIProgress();
        Powered = station.PoweredAndActive;
        EquipmentQuality = station.EquipmentQualityWithUpgrades;
        RecipeTypes = recipeTypes;
        Modifiers = modifiers;
    }

    /// <summary>The live station.</summary>
    public CraftingBase Station { get; }

    /// <summary>The station's id, see <see cref="CraftingStations.IdOf"/>.</summary>
    public string Id { get; }

    /// <summary>The game class: <c>manufacturing</c>, <c>manufacturingProcess</c>, <c>Growing</c>, <c>Dishwasher</c>, <c>Generator</c>.</summary>
    public string Kind { get; }

    /// <summary>The name the station shows in its window.</summary>
    public string Name { get; }

    public Vector3 Position { get; }

    public string Scene { get; }

    /// <summary>Something is being crafted or processed right now.</summary>
    public bool Busy { get; }

    /// <summary>Switched on, not broken, and powered (or needs no power).</summary>
    public bool Powered { get; }

    /// <summary>The station's equipment quality with its upgrades; better equipment crafts better and faster.</summary>
    public float EquipmentQuality { get; }

    /// <summary>The recipe types (<c>RecipeType</c> names) it can craft, upgrades included.</summary>
    public List<string> RecipeTypes { get; }

    /// <summary>The conditions it provides (<c>RecipeCondition</c> names), upgrades included.</summary>
    public List<string> Modifiers { get; }
}

/// <summary>A station started crafting, raised by <see cref="CraftingEvents.CraftStarted"/>.</summary>
public readonly struct CraftStart
{
    public CraftStart(int recipeId, StationInfo station, int multiples)
    {
        RecipeId = recipeId;
        Station = station;
        Multiples = multiples;
    }

    public int RecipeId { get; }

    public StationInfo Station { get; }

    /// <summary>How many times the recipe runs at once (stills and grow boxes can batch).</summary>
    public int Multiples { get; }
}

/// <summary>A craft finished, raised by <see cref="CraftingEvents.Crafted"/>.</summary>
public readonly struct CraftDone
{
    public CraftDone(int recipeId, StationInfo station, bool success, int multiples, List<ItemCount> results)
    {
        RecipeId = recipeId;
        Station = station;
        Success = success;
        Multiples = multiples;
        Results = results;
    }

    public int RecipeId { get; }

    public StationInfo Station { get; }

    /// <summary>False when the craft failed and gave the recipe's fail output instead.</summary>
    public bool Success { get; }

    public int Multiples { get; }

    /// <summary>What the recipe lists as its output (or fail output), times <see cref="Multiples"/>.</summary>
    public List<ItemCount> Results { get; }
}
