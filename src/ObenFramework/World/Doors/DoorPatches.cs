using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ObenFramework.World;

/// <summary>
/// Every door method that moves or locks a door, patched the same way. The private ones are the ones all
/// opens converge on (<c>OnOpen</c>) and the two that end a swing (<c>OnFullyOpen</c>, <c>OnFullyClosed</c>,
/// called from the door's own Update).
/// </summary>
[HarmonyPatch]
internal static class DoorPatches
{
    private static readonly string[] Methods =
    {
        "OnOpen", "Close", "ToggleInterrupt", "OnFullyOpen", "OnFullyClosed", "Lock", "Unlock", "ToggleLock",
    };

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        foreach (string name in Methods)
            yield return AccessTools.Method(typeof(Door), name) ?? throw new MissingMethodException(nameof(Door), name);
    }

    [HarmonyPrefix]
    private static void Before(Door __instance)
    {
        try { DoorWatch.Begin(__instance); }
        catch (Exception e) { WorldLog.Failed("Door hook (before)", e); }
    }

    // A finalizer rather than a postfix so the depth counter stays right when the game's code throws.
    [HarmonyFinalizer]
    private static Exception After(Exception __exception, Door __instance)
    {
        try { DoorWatch.End(__instance); }
        catch (Exception e) { WorldLog.Failed("Door hook (after)", e); }
        return __exception;
    }
}
