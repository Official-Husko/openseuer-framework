// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>
/// Changes single plants: harvesting into the player's inventory, resetting, ageing, pausing, rotting, and
/// planting into an empty spot. Every call refuses while a save loads and returns false for an unknown id.
/// </summary>
public static class PlantActions
{
    /// <summary>Harvests what is ripe into the player's inventory (overflow drops at their feet). True when anything was picked.</summary>
    public static bool Harvest(string plantId)
    {
        var plant = Usable(plantId);
        if (plant == null)
            return false;
        bool ready = HasReadyFruit(plant);
        using (WorldChanges.EnterMod())
        {
            var stacks = plant.HarvestExternal(out bool destroy);
            if (destroy && plant.growingSpot != null)
                plant.growingSpot.HarvestPlant();
            foreach (var stack in stacks)
            {
                if (stack != null && stack.itemReference != null)
                    ItemOperations.AddItemsAndDropRemaining(stack.itemReference.Item, stack.ownerId, null, stack.Meta, stack.itemAmount);
            }
            InventoryWatch.Check();
            return ready || stacks.Count > 0;
        }
    }

    /// <summary>Starts the plant over from a seedling.</summary>
    public static bool Reset(string plantId)
    {
        var plant = Usable(plantId);
        if (plant == null)
            return false;
        plant.ResetGrow();
        return true;
    }

    /// <summary>Sets how grown the plant is, in percent of its full-grown age.</summary>
    public static bool SetAgePercent(string plantId, int percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "Use 0 to 100.");
        var plant = Usable(plantId);
        if (plant == null)
            return false;
        plant.SetAgeFromPercentage(percent);
        return true;
    }

    /// <summary>Sets the age (game seconds) at which the plant is full grown; its current age is capped to it.</summary>
    public static bool SetFullGrownAge(string plantId, int age)
    {
        if (age < 1)
            throw new ArgumentOutOfRangeException(nameof(age), "The full grown age must be at least 1.");
        var plant = Usable(plantId);
        if (plant == null)
            return false;
        plant.FullGrownAge = age;
        plant.Age = Math.Min(plant.Age, age);
        return true;
    }

    public static bool SetPaused(string plantId, bool paused)
    {
        var plant = Usable(plantId);
        if (plant == null)
            return false;
        plant.Paused = paused;
        return true;
    }

    public static bool SetRotting(string plantId, bool rotting)
    {
        var plant = Usable(plantId);
        if (plant == null)
            return false;
        plant.Rotting = rotting;
        return true;
    }

    /// <summary>Plants a seed or seedling item into an empty spot. The item's pickup must be a growing plant.</summary>
    public static bool Plant(string spotId, int itemId)
    {
        var spot = ObjectIds.Find<GrowingSpot>(spotId);
        var item = ItemLookup.ById(itemId);
        if (spot == null || SaveGate.IsBusy || spot.HasPlant() || item == null || item.Prefab == null || item.Prefab.GetComponent<grow>() == null)
            return false;
        using (WorldChanges.EnterMod())
            spot.AddPlant(item);
        return spot.HasPlant();
    }

    /// <summary>Picks up what a spot holds when it is a finished pickable rather than a growing plant.</summary>
    public static bool HarvestSpot(string spotId)
    {
        var spot = ObjectIds.Find<GrowingSpot>(spotId);
        if (spot == null || SaveGate.IsBusy || !spot.HasPlant() || spot.Plant.GetComponent<grow>() != null)
            return false;
        var pickup = spot.Plant.GetComponentInChildren<CollectibleItem>();
        if (pickup == null)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (!pickup.PerformPickup())
                return false;
            spot.HarvestPlant();
            InventoryWatch.Check();
        }
        return true;
    }

    private static grow Usable(string plantId) => SaveGate.IsBusy ? null : Plants.Find(plantId);

    private static bool HasReadyFruit(grow plant)
    {
        if (plant.visibleGrownItems == null)
            return false;
        foreach (var fruit in plant.visibleGrownItems)
        {
            if (fruit != null && fruit.gameObject.activeInHierarchy && fruit.readyToPick)
                return true;
        }
        return false;
    }
}
