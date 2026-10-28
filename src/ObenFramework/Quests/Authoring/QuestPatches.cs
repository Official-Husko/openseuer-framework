// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Story;

namespace ObenFramework.Quests;

/// <summary>
/// Two game hooks the quest edits need. The game finds a started task again with <c>TaskInfo.Compare</c>,
/// which falls back to comparing titles; a mod's task (no asset path) is compared by id only, so it can never
/// merge with a game task or another mod's task that happens to share a title. And the game's four task
/// edit calls are counted while they run, so an edit made from inside one of them (a Harmony patch, a
/// relay reacting to the game's event) is refused instead of changing lists the game is walking.
/// </summary>
[HarmonyPatch]
internal static class QuestPatches
{
    private static int _depth;

    /// <summary>One of the game's task edit calls is running right now.</summary>
    public static bool InGameCall => _depth > 0;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskInfo), nameof(TaskInfo.Compare))]
    private static bool BeforeCompare(TaskInfo __instance, TaskInfo task, ref bool __result)
    {
        if (task == null)
            return true;
        try
        {
            if (!QuestIds.IsModTask(__instance.id, __instance.addressableAssetPath) && !QuestIds.IsModTask(task.id, task.addressableAssetPath))
                return true;
            __result = string.Equals(__instance.id, task.id, StringComparison.Ordinal);
            return false;
        }
        catch (Exception e)
        {
            StoryLog.Failed("TaskInfo.Compare hook", e);
            return true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTask))]
    private static void EnterEditTask() => _depth++;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTask))]
    private static void LeaveEditTask() => Leave();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTaskObjective))]
    private static void EnterEditObjective() => _depth++;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTaskObjective))]
    private static void LeaveEditObjective() => Leave();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTaskCounter))]
    private static void EnterEditCounter() => _depth++;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTaskCounter))]
    private static void LeaveEditCounter() => Leave();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTaskStatus), typeof(TaskStatusInfo), typeof(CurrentTaskStatus))]
    private static void EnterEditStatus() => _depth++;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TaskController), nameof(TaskController.EditTaskStatus), typeof(TaskStatusInfo), typeof(CurrentTaskStatus))]
    private static void LeaveEditStatus() => Leave();

    private static void Leave()
    {
        if (_depth > 0)
            _depth--;
    }

    /// <summary>A reload while a call ran (it threw past the finalizer) must not leave edits blocked.</summary>
    internal static void Reset() => _depth = 0;
}
