// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>A snapshot of a dishwasher.</summary>
public readonly struct DishwasherInfo
{
    public DishwasherInfo(string id, Vector3 position, bool busy, bool powered, float progress)
    {
        Id = id;
        Position = position;
        Busy = busy;
        Powered = powered;
        Progress = progress;
    }

    /// <summary>The station id, usable with <see cref="CraftingStations"/> too.</summary>
    public string Id { get; }

    public Vector3 Position { get; }

    /// <summary>A wash is running.</summary>
    public bool Busy { get; }

    public bool Powered { get; }

    /// <summary>How far the running wash is, 0 to 1 (0 when idle).</summary>
    public float Progress { get; }

    /// <summary>Powered and idle: a wash can start.</summary>
    public bool Ready => Powered && !Busy;
}

/// <summary>Dishwashers (a kind of crafting station that washes dishes over time): listing them, their contents, and hearing a wash finish.</summary>
public static class Dishwashers
{
    private static readonly AccessTools.FieldRef<manufacturing, List<CraftInProgress>> Running =
        AccessTools.FieldRefAccess<manufacturing, List<CraftInProgress>>("craftsInProgresses");

    /// <summary>A wash finished.</summary>
    public static readonly WorldEvent<DishwasherInfo> Finished = new WorldEvent<DishwasherInfo>(nameof(Finished));

    public static List<DishwasherInfo> All()
    {
        var list = new List<DishwasherInfo>();
        foreach (var dishwasher in ObjectIds.Active<Dishwasher>())
            list.Add(Describe(dishwasher));
        return list;
    }

    public static Dishwasher Find(string id) => CraftingStations.Find(id) as Dishwasher;

    /// <summary>What is in a dishwasher's slots.</summary>
    public static List<ContainerItem> Contents(string id)
    {
        var dishwasher = Find(id);
        return dishwasher != null ? Containers.GetItems(dishwasher.InputOutputStorage) : new List<ContainerItem>();
    }

    public static DishwasherInfo Describe(Dishwasher dishwasher) =>
        new DishwasherInfo(CraftingStations.IdOf(dishwasher), dishwasher.transform.position, dishwasher.CraftingIProgress(), dishwasher.PoweredAndActive, Progress(dishwasher));

    private static float Progress(Dishwasher dishwasher)
    {
        var running = Running(dishwasher);
        if (running == null || running.Count == 0 || running[0] == null || running[0].timer == null || running[0].timer.timeInSeconds <= 0)
            return 0f;
        var timer = running[0].timer;
        return Mathf.Clamp01(1f - (float)timer.remainingInSeconds / timer.timeInSeconds);
    }
}

[HarmonyPatch]
internal static class DishwashersPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(manufacturing), nameof(manufacturing.CraftDelayed))]
    private static void Finished(manufacturing __instance)
    {
        if (!(__instance is Dishwasher dishwasher) || !Dishwashers.Finished.HasSubscribers || !WorldChanges.Active)
            return;
        try { Dishwashers.Finished.Raise(Dishwashers.Describe(dishwasher)); }
        catch (Exception e) { ItemsLog.Failed("Dishwasher hook", e); }
    }
}
