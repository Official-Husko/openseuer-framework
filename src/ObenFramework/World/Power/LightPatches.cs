using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ObenFramework.World;

/// <summary>Raises <see cref="Lights.LightChanged"/> from the public methods that switch a fixture (the fading switch-off included).</summary>
[HarmonyPatch]
internal static class LightPatches
{
    private const int On = 1;
    private const int NoPower = 2;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(LightTurnOffOn), nameof(LightTurnOffOn.turnLightOn));
        yield return AccessTools.Method(typeof(LightTurnOffOn), nameof(LightTurnOffOn.turnLightOff));
        yield return AccessTools.Method(typeof(LightTurnOffOn), nameof(LightTurnOffOn.TogglePower));
        yield return AccessTools.Method(typeof(LightTurnOffOn), nameof(LightTurnOffOn.TurnLightOffFade));
    }

    [HarmonyPrefix]
    private static void Before(LightTurnOffOn __instance, out int __state) =>
        __state = (__instance.on ? On : 0) | (__instance.noPower ? NoPower : 0);

    [HarmonyPostfix]
    private static void After(LightTurnOffOn __instance, int __state)
    {
        try
        {
            if (!Lights.LightChanged.HasSubscribers || !WorldChanges.Active)
                return;

            bool wasOn = (__state & On) != 0;
            bool hadNoPower = (__state & NoPower) != 0;
            var source = WorldChanges.Current;
            if (__instance.on != wasOn)
            {
                var kind = __instance.on ? LightChangeKind.TurnedOn : LightChangeKind.TurnedOff;
                Lights.LightChanged.Raise(new LightChange(__instance, kind, source));
            }
            if (__instance.noPower != hadNoPower)
            {
                var kind = __instance.noPower ? LightChangeKind.PowerLost : LightChangeKind.PowerRestored;
                Lights.LightChanged.Raise(new LightChange(__instance, kind, source));
            }
        }
        catch (Exception e)
        {
            WorldLog.Failed("Light hook", e);
        }
    }
}
