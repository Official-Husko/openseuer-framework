// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Building;

/// <summary>
/// Puts custom blueprints into the pools the game draws found blueprints from (the general pool, the first
/// finds, and the level's named categories). The game's pools are shared assets, so the framework adds one
/// list of its own to each and takes it out again when the blueprints go away or the framework unloads.
/// </summary>
internal static class FurniturePools
{
    public const string General = "";
    public const string Start = "Start";

    private static readonly AccessTools.FieldRef<FurnitureBlueprintController, TaskItemBlueprintListMain> StartMain =
        AccessTools.FieldRefAccess<FurnitureBlueprintController, TaskItemBlueprintListMain>("startBlueprintToSpawn");
    private static readonly AccessTools.FieldRef<FurnitureBlueprintController, TaskItemBlueprintListMain> GeneralMain =
        AccessTools.FieldRefAccess<FurnitureBlueprintController, TaskItemBlueprintListMain>("blueprintsToSpawn");
    private static readonly AccessTools.FieldRef<FurnitureBlueprintController, List<FurnitureBlueprintController.BlueprintCategory>> Categories =
        AccessTools.FieldRefAccess<FurnitureBlueprintController, List<FurnitureBlueprintController.BlueprintCategory>>("blueprintCategoriesToAdd");
    private static readonly AccessTools.FieldRef<FurnitureBlueprintController, List<TaskItemBlueprint>> StartLive =
        AccessTools.FieldRefAccess<FurnitureBlueprintController, List<TaskItemBlueprint>>("startBlueprints");
    private static readonly AccessTools.FieldRef<FurnitureBlueprintController, List<TaskItemBlueprint>> GeneralLive =
        AccessTools.FieldRefAccess<FurnitureBlueprintController, List<TaskItemBlueprint>>("blueprints");
    private static readonly AccessTools.FieldRef<FurnitureBlueprintController, bool> StartDone =
        AccessTools.FieldRefAccess<FurnitureBlueprintController, bool>("startDone");

    private static readonly Dictionary<TaskItemBlueprintListMain, TaskItemBlueprintList> Ours = new Dictionary<TaskItemBlueprintListMain, TaskItemBlueprintList>();

    /// <summary>Blueprints were added or removed: refill the pools of the current level now.</summary>
    public static void Changed()
    {
        var controller = FurnitureBlueprintController.instance;
        if (controller != null)
            Fill(controller);
    }

    /// <summary>Takes the framework's lists out of the game's pools.</summary>
    public static void Reset()
    {
        foreach (var pair in Ours)
        {
            if (pair.Key != null)
                pair.Key.taskItemBlueprintLists.Remove(pair.Value);
            if (pair.Value != null)
                Object.Destroy(pair.Value);
        }
        Ours.Clear();
    }

    private static void Fill(FurnitureBlueprintController controller)
    {
        var groups = new Dictionary<TaskItemBlueprintListMain, List<TaskItemBlueprint>>();
        foreach (var main in Ours.Keys)
        {
            if (main != null)
                groups[main] = new List<TaskItemBlueprint>();
        }

        foreach (var entry in FurnitureBlueprints.All())
        {
            if (entry.Definition.Pools.Count == 0)
                continue;
            var blueprint = FurnitureBlueprints.Registered(entry);
            if (blueprint == null)
                continue;
            foreach (string pool in entry.Definition.Pools)
            {
                var main = MainFor(controller, pool);
                if (main == null)
                    continue;
                if (!groups.TryGetValue(main, out var group))
                    groups[main] = group = new List<TaskItemBlueprint>();
                if (!group.Contains(blueprint))
                    group.Add(blueprint);
                if (StartDone(controller))
                    AddLive(controller, pool, blueprint);
            }
        }

        foreach (var pair in groups)
        {
            var list = ListFor(pair.Key);
            list.blueprints.Clear();
            list.blueprints.AddRange(pair.Value);
            if (!pair.Key.taskItemBlueprintLists.Contains(list))
                pair.Key.taskItemBlueprintLists.Add(list);
        }
    }

    // The game copied its pools when the level started; add new blueprints the player hasn't found yet.
    private static void AddLive(FurnitureBlueprintController controller, string pool, TaskItemBlueprint blueprint)
    {
        var live = pool == General ? GeneralLive(controller) : pool == Start ? StartLive(controller) : null;
        if (live != null && !live.Contains(blueprint) && !controller.BlueprintIsCollected(blueprint))
            live.Add(blueprint);
    }

    private static TaskItemBlueprintListMain MainFor(FurnitureBlueprintController controller, string pool)
    {
        if (pool == General)
            return GeneralMain(controller);
        if (pool == Start)
            return StartMain(controller);
        var categories = Categories(controller);
        if (categories != null)
        {
            foreach (var category in categories)
            {
                if (category != null && category.name == pool)
                    return category.blueprints;
            }
        }
        FurnitureRegistry.Log.Warn("pool " + pool, $"the level has no blueprint category '{pool}'; blueprints for it aren't added here");
        return null;
    }

    private static TaskItemBlueprintList ListFor(TaskItemBlueprintListMain main)
    {
        if (Ours.TryGetValue(main, out var list) && list != null)
            return list;
        list = ScriptableObject.CreateInstance<TaskItemBlueprintList>();
        list.name = "Mod blueprints";
        list.blueprints = new List<TaskItemBlueprint>();
        list.hideFlags = HideFlags.HideAndDontSave;
        Ours[main] = list;
        return list;
    }

    // Before the controller copies its pools (when a level starts or a save loads).
    [HarmonyPatch(typeof(FurnitureBlueprintController), "Start")]
    private static class StartPatch
    {
        [HarmonyPrefix]
        private static void Before(FurnitureBlueprintController __instance)
        {
            try { Fill(__instance); }
            catch (Exception e) { FurnitureRegistry.Log.Error("Blueprint pool hook", e); }
        }
    }

    // A save names the blueprints already spawned in the world by id; let it find the mod ones.
    [HarmonyPatch(typeof(TaskItemBlueprintListMain), nameof(TaskItemBlueprintListMain.GetBlueprint))]
    private static class LookupPatch
    {
        [HarmonyPostfix]
        private static void After(string id, ref TaskItemBlueprint __result)
        {
            try
            {
                if (__result != null || FurnitureIds.Classify(id) != FurnitureIdKind.Blueprint)
                    return;
                var entry = FurnitureBlueprints.Find(id);
                if (entry != null)
                    __result = FurnitureBlueprints.Registered(entry);
            }
            catch (Exception e) { FurnitureRegistry.Log.Error("Blueprint lookup hook", e); }
        }
    }
}
