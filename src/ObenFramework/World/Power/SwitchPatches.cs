using System;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// Raises <see cref="Switches.SwitchChanged"/> from <c>PressEffects</c> and <c>UnpressEffects</c>, the two
/// methods every press path ends in (a key press, <c>Press()</c> from a script, a timed release).
/// </summary>
/// <remarks>
/// A switch's press runs in a coroutine after the interact call has returned, so the player scope of the
/// interact hook is long closed by then. The hook remembers which switch the player used last and, while
/// the effects run, opens the player scope again so lights and doors the switch drives are attributed to
/// the player too.
/// </remarks>
[HarmonyPatch]
internal static class SwitchPatches
{
    private const float PlayerWindowSeconds = 5f;

    private static Button _used;
    private static float _usedAt;

    internal struct Effect
    {
        public bool Scoped;
        public ChangeSource Source;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Button), nameof(Button.Interact))]
    private static void Pressed(Button __instance) => MarkUsed(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Button), nameof(Button.StopInteract))]
    private static void Released(Button __instance) => MarkUsed(__instance);

    private static void MarkUsed(Button button)
    {
        _used = button;
        _usedAt = Time.unscaledTime;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Button), nameof(Button.PressEffects))]
    private static void BeforePress(Button __instance, out Effect __state) => __state = Begin(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Button), nameof(Button.UnpressEffects))]
    private static void BeforeUnpress(Button __instance, out Effect __state) => __state = Begin(__instance);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Button), nameof(Button.PressEffects))]
    private static Exception AfterPress(Exception __exception, Button __instance, Effect __state) => End(__exception, __instance, __state, true);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Button), nameof(Button.UnpressEffects))]
    private static Exception AfterUnpress(Exception __exception, Button __instance, Effect __state) => End(__exception, __instance, __state, false);

    private static Effect Begin(Button button)
    {
        var effect = new Effect { Source = WorldChanges.Current };
        try
        {
            if (effect.Source == ChangeSource.Game && _used == button && Time.unscaledTime - _usedAt < PlayerWindowSeconds)
            {
                effect.Source = ChangeSource.LocalPlayer;
                effect.Scoped = true;
                WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);
            }
        }
        catch (Exception e)
        {
            WorldLog.Failed("Switch hook (before)", e);
        }
        return effect;
    }

    private static Exception End(Exception exception, Button button, Effect effect, bool pressed)
    {
        try
        {
            if (effect.Scoped)
                WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
            if (exception == null && Switches.SwitchChanged.HasSubscribers && WorldChanges.Active)
                Switches.SwitchChanged.Raise(new SwitchChange(button, pressed, effect.Source));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Switch hook (after)", e);
        }
        return exception;
    }
}
