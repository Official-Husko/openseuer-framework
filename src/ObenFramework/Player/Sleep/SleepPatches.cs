// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// Raises <see cref="PlayerSleep"/>'s events from the game's <c>WaitingController</c>. Every bed, bench,
/// pass-out and arrest goes through StartSleep or Wait, and every end through Wakeup.
/// </summary>
[HarmonyPatch]
internal static class SleepPatches
{
    internal static readonly AccessTools.FieldRef<WaitingController, int> AlarmSeconds =
        AccessTools.FieldRefAccess<WaitingController, int>("currentSleepSetTime");

    internal struct Rest
    {
        public bool Resting;
        public WaitingController.Type Kind;
        public int Seconds;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WaitingController), "StartSleep")]
    private static void AfterStartSleep(WaitingController __instance)
    {
        try
        {
            if (!__instance.IsSleeping)
                return;
            int alarm = __instance.alarmEnabled ? AlarmSeconds(__instance) : -1;
            bool passedOut = __instance.waitingType != WaitingController.Type.Sleeping;
            PlayerSleep.SleepStarted.Raise(new SleepStart(alarm, passedOut), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting the start of a sleep failed", e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.Wait))]
    private static void BeforeWait() => ModWait.GameWaitStarting();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.Wait))]
    private static void AfterWait(WaitingController __instance)
    {
        if (__instance.IsWaiting)
            PlayerSleep.WaitStarted.Raise(__instance.waitingType, GameEvents.Log);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.Wakeup))]
    private static void BeforeWakeup(WaitingController __instance, out Rest __state) => __state = new Rest
    {
        Resting = __instance.IsSleeping || __instance.IsWaiting,
        Kind = __instance.waitingType,
        Seconds = __instance.sleepingTimeSeconds,
    };

    // A finalizer, so a wake-up that throws halfway (a missing UI in some scene) still ends the mod's wait.
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.Wakeup))]
    private static Exception AfterWakeup(Exception __exception, WaitingController __instance, bool alarm, Rest __state)
    {
        try
        {
            if (__state.Resting && !__instance.IsSleeping && !__instance.IsWaiting)
            {
                ModWait.Ended(__state.Seconds);
                // The game ignores the alarm flag for waits, so only a sleep counts as woken by it.
                bool slept = __state.Kind == WaitingController.Type.Sleeping || __state.Kind == WaitingController.Type.Passout;
                PlayerSleep.WokeUp.Raise(new RestEnd(__state.Kind, __state.Seconds, alarm && slept), GameEvents.Log);
            }
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a wake-up failed", e);
        }
        return __exception;
    }
}
