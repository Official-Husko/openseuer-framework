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

/// <summary>Keeps forced flags out of the save: the values underneath are written while <c>GlobalState</c> serialises.</summary>
[HarmonyPatch(typeof(GlobalState), nameof(GlobalState.OnSavingGame))]
internal static class FlagSavePatch
{
    [HarmonyPrefix]
    private static void Before(GlobalState __instance)
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
    private static void After(GlobalState __instance)
    {
        try
        {
            FlagOverrides.ResumeAfterSave(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Global flag save hook", e);
        }
    }
}
