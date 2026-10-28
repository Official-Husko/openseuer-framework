// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>A snapshot of an autofarm: a field that replants itself (the farms NPCs harvest).</summary>
public readonly struct AutofarmInfo
{
    public AutofarmInfo(string id, Vector3 position, int slots, int planted, List<int> plantItemIds, bool plantInOrder, int growMin, int growMax, int ownerId)
    {
        Id = id;
        Position = position;
        Slots = slots;
        Planted = planted;
        PlantItemIds = plantItemIds;
        PlantInOrder = plantInOrder;
        GrowPercentMin = growMin;
        GrowPercentMax = growMax;
        OwnerId = ownerId;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>How many plants the field holds.</summary>
    public int Slots { get; }

    /// <summary>How many slots have a plant now.</summary>
    public int Planted { get; }

    /// <summary>The plant items it picks from when replanting.</summary>
    public List<int> PlantItemIds { get; }

    public bool PlantInOrder { get; }

    /// <summary>A new plant starts this far grown, in percent (a random value in the range).</summary>
    public int GrowPercentMin { get; }

    public int GrowPercentMax { get; }

    /// <summary>The character the crop belongs to, or 0.</summary>
    public int OwnerId { get; }
}

/// <summary>An autofarm planted, raised by <see cref="Autofarms.Replanted"/>.</summary>
public readonly struct ReplantInfo
{
    public ReplantInfo(string autofarmId, int planted, bool initial)
    {
        AutofarmId = autofarmId;
        Planted = planted;
        Initial = initial;
    }

    public string AutofarmId { get; }

    /// <summary>How many plants it put in.</summary>
    public int Planted { get; }

    /// <summary>The first planting when the field starts, not a replant.</summary>
    public bool Initial { get; }
}

/// <summary>Autofarms in the loaded scenes: listing them, changing how grown new plants start, and hearing them replant.</summary>
public static class Autofarms
{
    /// <summary>An autofarm put new plants in.</summary>
    public static readonly WorldEvent<ReplantInfo> Replanted = new WorldEvent<ReplantInfo>(nameof(Replanted));

    public static List<AutofarmInfo> All()
    {
        var list = new List<AutofarmInfo>();
        foreach (var farm in ObjectIds.Active<Autofarm>())
            list.Add(Describe(farm));
        return list;
    }

    public static Autofarm Find(string id) => ObjectIds.Find<Autofarm>(id);

    /// <summary>Sets how grown new plants start, in percent (0 to 100, min at most max).</summary>
    public static bool SetGrowPercent(string id, int min, int max)
    {
        if (min < 0 || min > 100 || max < min || max > 100)
            throw new ArgumentOutOfRangeException(nameof(min), "Use 0 <= min <= max <= 100.");
        var farm = Find(id);
        if (farm == null)
            return false;
        farm.growPercentageMin = min;
        farm.growPercentageMax = max;
        return true;
    }

    public static AutofarmInfo Describe(Autofarm farm)
    {
        var items = new List<int>();
        if (farm.plantPrefabs != null)
        {
            foreach (var reference in farm.plantPrefabs)
            {
                if (reference != null && reference.ID > 0)
                    items.Add(reference.ID);
            }
        }
        return new AutofarmInfo(ObjectIds.IdOf(farm), farm.transform.position, farm.plants != null ? farm.plants.Length : 0, CountPlanted(farm), items,
            farm.plantInOrder, farm.growPercentageMin, farm.growPercentageMax, farm.ownerId != null ? farm.ownerId.ID : 0);
    }

    internal static int CountPlanted(Autofarm farm)
    {
        if (farm.plants == null)
            return 0;
        int count = 0;
        foreach (var plant in farm.plants)
        {
            if (plant != null)
                count++;
        }
        return count;
    }
}

[HarmonyPatch]
internal static class AutofarmsPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Autofarm), "Plant")]
    private static void Planting(Autofarm __instance, out int __state) =>
        __state = Autofarms.Replanted.HasSubscribers ? Autofarms.CountPlanted(__instance) : -1;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Autofarm), "Plant")]
    private static void Planted(Autofarm __instance, int __state, bool ___initDone)
    {
        if (__state < 0 || !WorldChanges.Active)
            return;
        try
        {
            int planted = Autofarms.CountPlanted(__instance) - __state;
            if (planted > 0)
                Autofarms.Replanted.Raise(new ReplantInfo(ObjectIds.IdOf(__instance), planted, !___initDone));
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Autofarm hook", e);
        }
    }
}
