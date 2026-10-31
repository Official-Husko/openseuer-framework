using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>Raises <see cref="GlobalFlags.FlagChanged"/> from the three <c>GlobalState.SetState</c> overloads and keeps forces on top of game writes.</summary>
[HarmonyPatch]
internal static class FlagPatches
{
    private const int Skip = -1;
    private const int Existed = 1;
    private const int WasOn = 2;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(GlobalState), nameof(GlobalState.SetState), new[] { typeof(string) });
        yield return AccessTools.Method(typeof(GlobalState), nameof(GlobalState.SetState), new[] { typeof(string), typeof(bool) });
        yield return AccessTools.Method(typeof(GlobalState), nameof(GlobalState.SetState), new[] { typeof(string), typeof(bool), typeof(object) });
    }

    [HarmonyPrefix]
    private static void Before(GlobalState __instance, string key, out int __state)
    {
        __state = Skip;
        if (key == null || (!GlobalFlags.FlagChanged.HasSubscribers && !FlagOverrides.Any))
            return;
        var state = __instance.state;
        __state = state.TryGetValue(key, out var info) && info != null ? Existed | (info.active ? WasOn : 0) : 0;
    }

    [HarmonyPostfix]
    private static void After(GlobalState __instance, string key, int __state)
    {
        if (__state == Skip)
            return;
        try
        {
            FlagOverrides.GameWrote(__instance, key);
            bool existed = (__state & Existed) != 0;
            bool was = (__state & WasOn) != 0;
            bool now = __instance.state.TryGetValue(key, out var info) && info != null && info.active;
            if (now != was || !existed)
                FlagOverrides.Raise(key, now, was, true, WorldChanges.Current);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Global flag hook", e);
        }
    }
}

/// <summary>
/// Keeps forced flags out of the save. <c>GlobalState.OnSavingGame</c> only hands the object to the save; the
/// file is written at the end of <c>SaveController.SaveGame</c>. So the values underneath go in when the game
/// collects the flags and the forces come back once <c>SaveGame</c> returns or throws.
/// </summary>
[HarmonyPatch]
internal static class FlagSavePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GlobalState), nameof(GlobalState.OnSavingGame))]
    private static void Collecting(GlobalState __instance)
    {
        try
        {
            FlagOverrides.SuspendForSave(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Global flag save hook", e);
        }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SaveController), nameof(SaveController.SaveGame))]
    private static Exception Written(Exception __exception)
    {
        try
        {
            FlagOverrides.ResumeAfterSave();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Global flag save hook", e);
        }
        return __exception;
    }
}
