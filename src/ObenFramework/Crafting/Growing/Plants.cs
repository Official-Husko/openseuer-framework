// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>
/// Growing plants in the loaded scenes (the game's <c>grow</c>, in grow boxes, planters and fields): listing
/// them with their fruit, and hearing harvests and picks. A plant's id is its own save GUID, so it stays the
/// same while the plant lives. <see cref="PlantActions"/> changes them.
/// </summary>
public static class Plants
{
    private static readonly AccessTools.FieldRef<GrowItem, grow.ItemAmount> FruitItem =
        AccessTools.FieldRefAccess<GrowItem, grow.ItemAmount>("item");

    private static readonly AccessTools.FieldRef<GrowItem, int> FruitAmount =
        AccessTools.FieldRefAccess<GrowItem, int>("itemAmount");

    private static readonly AccessTools.FieldRef<GrowItem, int> FruitFullGrownAge =
        AccessTools.FieldRefAccess<GrowItem, int>("fullGrownAgeFinal");

    private static readonly AccessTools.FieldRef<GrowItem, grow> FruitPlant =
        AccessTools.FieldRefAccess<GrowItem, grow>("grow");

    /// <summary>A plant was harvested and gave items (picks of single fruit are <see cref="Picked"/>).</summary>
    public static readonly WorldEvent<PlantHarvest> Harvested = new WorldEvent<PlantHarvest>(nameof(Harvested));

    /// <summary>A ripe fruit was picked from a plant.</summary>
    public static readonly WorldEvent<PlantPick> Picked = new WorldEvent<PlantPick>(nameof(Picked));

    public static List<PlantInfo> All()
    {
        var list = new List<PlantInfo>();
        foreach (var plant in SceneIds.Active<grow>())
            list.Add(Describe(plant));
        return list;
    }

    /// <summary>The plant's id: its save GUID, or its scene id when it has none.</summary>
    public static string IdOf(grow plant) =>
        plant == null ? null : !string.IsNullOrEmpty(plant.GUID) ? plant.GUID : SceneIds.IdOf(plant);

    public static grow Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var plant in SceneIds.Active<grow>())
        {
            if (plant.GUID == id)
                return plant;
        }
        return SceneIds.Find<grow>(id);
    }

    public static PlantInfo Describe(grow plant) =>
        new PlantInfo(IdOf(plant), plant.transform.position, plant.Age, plant.FullGrownAge, plant.GetCurrentStageProcess(), plant.Paused, plant.Rotting, ReadyFruit(plant));

    /// <summary>The fruit of a plant (empty for plants harvested whole).</summary>
    public static List<FruitInfo> Fruit(string plantId)
    {
        var list = new List<FruitInfo>();
        var plant = Find(plantId);
        if (plant == null || plant.visibleGrownItems == null)
            return list;
        foreach (var fruit in plant.visibleGrownItems)
        {
            if (fruit != null)
                list.Add(DescribeFruit(fruit));
        }
        return list;
    }

    internal static grow PlantOf(GrowItem fruit) => FruitPlant(fruit);

    internal static int ItemOf(GrowItem fruit)
    {
        var item = FruitItem(fruit);
        return item != null && item.item != null ? item.item.ID : -1;
    }

    internal static int AmountOf(GrowItem fruit) => FruitAmount(fruit);

    private static FruitInfo DescribeFruit(GrowItem fruit)
    {
        int fullGrown = FruitFullGrownAge(fruit);
        float spoil = fruit.GetCurrentSpoilTime();
        return new FruitInfo(ItemOf(fruit), FruitAmount(fruit), fruit.gameObject.activeSelf, fruit.readyToPick, fruit.Spoiled(),
            fullGrown > 0 ? Mathf.Clamp01((float)fruit.age / fullGrown) : 0f, float.IsNaN(spoil) ? 0f : spoil);
    }

    private static int ReadyFruit(grow plant)
    {
        int count = 0;
        if (plant.visibleGrownItems == null)
            return 0;
        foreach (var fruit in plant.visibleGrownItems)
        {
            if (fruit != null && fruit.gameObject.activeSelf && fruit.readyToPick)
                count++;
        }
        return count;
    }
}
