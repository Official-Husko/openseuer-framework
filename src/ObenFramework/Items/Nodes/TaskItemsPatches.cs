// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// Reports task item changes by comparing the list before and after the outermost add or remove call (the
/// game nests them). Only while someone listens: the snapshot is the only allocation.
/// </summary>
[HarmonyPatch]
internal static class TaskItemsPatches
{
    private static int _depth;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskItemsManager), nameof(TaskItemsManager.AddTaskItem))]
    private static void Adding(TaskItemsManager __instance, bool updateUiOnly, out Dictionary<TaskItemsManager.TaskItemInfo, int> __state) =>
        __state = updateUiOnly ? Enter(null) : Enter(__instance);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TaskItemsManager), nameof(TaskItemsManager.AddTaskItem))]
    private static void Added(TaskItemsManager __instance, Dictionary<TaskItemsManager.TaskItemInfo, int> __state) => Leave(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskItemsManager), nameof(TaskItemsManager.RemoveItem))]
    private static void Removing(TaskItemsManager __instance, out Dictionary<TaskItemsManager.TaskItemInfo, int> __state) =>
        __state = Enter(__instance);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TaskItemsManager), nameof(TaskItemsManager.RemoveItem))]
    private static void Removed(TaskItemsManager __instance, Dictionary<TaskItemsManager.TaskItemInfo, int> __state) => Leave(__instance, __state);

    // Every call counts for the depth; only the outermost one with a listener takes a snapshot.
    private static Dictionary<TaskItemsManager.TaskItemInfo, int> Enter(TaskItemsManager manager)
    {
        _depth++;
        if (_depth > 1 || manager == null || !TaskItems.Changed.HasSubscribers || !WorldChanges.Active)
            return null;
        var before = new Dictionary<TaskItemsManager.TaskItemInfo, int>();
        foreach (var entry in manager.taskItems)
        {
            if (entry != null)
                before[entry] = entry.amount;
        }
        return before;
    }

    private static void Leave(TaskItemsManager manager, Dictionary<TaskItemsManager.TaskItemInfo, int> before)
    {
        _depth = Math.Max(0, _depth - 1);
        if (before == null)
            return;
        try
        {
            foreach (var change in Changes(manager, before))
                TaskItems.Changed.Raise(change);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Task item hook", e);
        }
    }

    private static List<TaskItemChange> Changes(TaskItemsManager manager, Dictionary<TaskItemsManager.TaskItemInfo, int> before)
    {
        var changes = new List<TaskItemChange>();
        var now = new HashSet<TaskItemsManager.TaskItemInfo>();
        foreach (var entry in manager.taskItems)
        {
            if (entry == null || entry.taskItem == null)
                continue;
            now.Add(entry);
            int old = before.TryGetValue(entry, out int amount) ? amount : 0;
            if (!before.ContainsKey(entry) || old != entry.amount)
                changes.Add(Change(entry.taskItem, old, entry.amount, true));
        }
        foreach (var pair in before)
        {
            if (pair.Key.taskItem != null && !now.Contains(pair.Key))
                changes.Add(Change(pair.Key.taskItem, pair.Value, 0, false));
        }
        return changes;
    }

    private static TaskItemChange Change(TaskItem item, int oldAmount, int newAmount, bool held) =>
        new TaskItemChange(item.Id, item.itemName, item.itemType.ToString(), oldAmount, newAmount, held);
}
