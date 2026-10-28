using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>Raises the fixture events (lanterns, flood gates, showers, saunas, lights with several states). Each hook returns at once while nobody listens.</summary>
[HarmonyPatch]
internal static class FixturePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Lantern), nameof(Lantern.CandleInserted))]
    private static void BeforeLit(Lantern __instance, out bool __state) => __state = Lanterns.IsLit(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Lantern), nameof(Lantern.CandleInserted))]
    private static void AfterLit(Lantern __instance, bool __state)
    {
        if (!__state && Listening(Lanterns.LanternChanged.HasSubscribers))
            Raise(() => Lanterns.LanternChanged.Raise(new LanternChange(__instance, LanternChangeKind.Lit, WorldChanges.Current)));
    }

    // -1: was not lit; 0: burnt down (the timer ran out); 1: blown out early.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Lantern), nameof(Lantern.CandleExtinguished))]
    private static void BeforeOut(Lantern __instance, out int __state) =>
        __state = __instance.candleTimer == null ? -1 : __instance.candleTimer.remainingInSeconds > 0 ? 1 : 0;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Lantern), nameof(Lantern.CandleExtinguished))]
    private static void AfterOut(Lantern __instance, int __state)
    {
        if (__state < 0 || !Listening(Lanterns.LanternChanged.HasSubscribers))
            return;
        var kind = __state == 0 ? LanternChangeKind.BurntOut : LanternChangeKind.Extinguished;
        Raise(() => Lanterns.LanternChanged.Raise(new LanternChange(__instance, kind, WorldChanges.Current)));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FloodGateControl), nameof(FloodGateControl.OpenFloodGate))]
    private static void BeforeOpen(FloodGateControl __instance, out bool __state) => __state = FloodGates.IsOpen(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FloodGateControl), nameof(FloodGateControl.OpenFloodGate))]
    private static void AfterOpen(FloodGateControl __instance, bool __state) => GateMoved(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FloodGateControl), nameof(FloodGateControl.CloseFloodGate))]
    private static void BeforeClose(FloodGateControl __instance, out bool __state) => __state = FloodGates.IsOpen(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FloodGateControl), nameof(FloodGateControl.CloseFloodGate))]
    private static void AfterClose(FloodGateControl __instance, bool __state) => GateMoved(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InteractableShower), nameof(InteractableShower.TurnOn), new[] { typeof(bool), typeof(bool) })]
    private static void BeforeShowerOn(InteractableShower __instance, out bool __state) => __state = Showers.IsOn(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractableShower), nameof(InteractableShower.TurnOn), new[] { typeof(bool), typeof(bool) })]
    private static void AfterShowerOn(InteractableShower __instance, bool __state) => ShowerSwitched(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InteractableShower), nameof(InteractableShower.TurnOff), new[] { typeof(bool) })]
    private static void BeforeShowerOff(InteractableShower __instance, out bool __state) => __state = Showers.IsOn(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractableShower), nameof(InteractableShower.TurnOff), new[] { typeof(bool) })]
    private static void AfterShowerOff(InteractableShower __instance, bool __state) => ShowerSwitched(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Sauna), nameof(Sauna.TurnOn))]
    private static void BeforeSaunaOn(Sauna __instance, out bool __state) => __state = __instance.On;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Sauna), nameof(Sauna.TurnOn))]
    private static void AfterSaunaOn(Sauna __instance, bool __state) => SaunaSwitched(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Sauna), nameof(Sauna.TurnOff))]
    private static void BeforeSaunaOff(Sauna __instance, out bool __state) => __state = __instance.On;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Sauna), nameof(Sauna.TurnOff))]
    private static void AfterSaunaOff(Sauna __instance, bool __state) => SaunaSwitched(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LightTurnOffOnMultiState), nameof(LightTurnOffOnMultiState.Toggle))]
    private static void BeforeToggle(LightTurnOffOnMultiState __instance, out int __state) => __state = Pack(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LightTurnOffOnMultiState), nameof(LightTurnOffOnMultiState.Toggle))]
    private static void AfterToggle(LightTurnOffOnMultiState __instance, int __state) => LightChanged(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LightTurnOffOnMultiState), nameof(LightTurnOffOnMultiState.SetState))]
    private static void BeforeState(LightTurnOffOnMultiState __instance, out int __state) => __state = Pack(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LightTurnOffOnMultiState), nameof(LightTurnOffOnMultiState.SetState))]
    private static void AfterState(LightTurnOffOnMultiState __instance, int __state) => LightChanged(__instance, __state);

    private static bool Listening(bool subscribed) => subscribed && WorldChanges.Active;

    private static void GateMoved(FloodGateControl gate, bool wasOpen)
    {
        bool open = FloodGates.IsOpen(gate);
        if (open != wasOpen && Listening(FloodGates.GateChanged.HasSubscribers))
            Raise(() => FloodGates.GateChanged.Raise(new FloodGateChange(gate, open, WorldChanges.Current)));
    }

    private static void ShowerSwitched(InteractableShower shower, bool wasOn)
    {
        bool on = Showers.IsOn(shower);
        if (on != wasOn && Listening(Showers.ShowerChanged.HasSubscribers))
            Raise(() => Showers.ShowerChanged.Raise(new ShowerChange(shower, on, WorldChanges.Current)));
    }

    private static void SaunaSwitched(Sauna sauna, bool wasOn)
    {
        if (sauna.On != wasOn && Listening(Saunas.SaunaChanged.HasSubscribers))
            Raise(() => Saunas.SaunaChanged.Raise(new SaunaChange(sauna, sauna.On, WorldChanges.Current)));
    }

    // On/off in the low bit, the state (shifted past -1) above it.
    private static int Pack(LightTurnOffOnMultiState light) => ((light.currentState + 1) << 1) | (light.on ? 1 : 0);

    private static void LightChanged(LightTurnOffOnMultiState light, int before)
    {
        if (Pack(light) != before && Listening(MultiStateLights.StateChanged.HasSubscribers))
            Raise(() => MultiStateLights.StateChanged.Raise(new LightStateChange(light, light.on, light.currentState, WorldChanges.Current)));
    }

    // Only reached when someone listens and something changed, so the closure is not a per-frame cost.
    private static void Raise(Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Fixture hook", e);
        }
    }
}
