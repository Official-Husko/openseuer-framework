using System;
using HarmonyLib;

namespace ObenFramework.World;

/// <summary>Raises <see cref="PowerGrids.GridChanged"/> from the grid's two setters.</summary>
[HarmonyPatch]
internal static class PowerGridPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerGrid), nameof(PowerGrid.Toggle))]
    private static void BeforeToggle(PowerGrid __instance, out bool __state) => __state = __instance.GridEnabled;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerGrid), nameof(PowerGrid.Toggle))]
    private static void AfterToggle(PowerGrid __instance, bool __state)
    {
        if (__instance.GridEnabled != __state)
            Raise(__instance, PowerGridChangeKind.Toggled);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerGrid), nameof(PowerGrid.SetBaseBalance))]
    private static void BeforeCapacity(PowerGrid __instance, out int __state) => __state = __instance.BaseCapacity;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerGrid), nameof(PowerGrid.SetBaseBalance))]
    private static void AfterCapacity(PowerGrid __instance, int __state)
    {
        if (__instance.BaseCapacity != __state)
            Raise(__instance, PowerGridChangeKind.CapacityChanged);
    }

    private static void Raise(PowerGrid grid, PowerGridChangeKind kind)
    {
        try
        {
            if (PowerGrids.GridChanged.HasSubscribers && WorldChanges.Active)
                PowerGrids.GridChanged.Raise(new PowerGridChange(grid, kind, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Power grid hook", e);
        }
    }
}
