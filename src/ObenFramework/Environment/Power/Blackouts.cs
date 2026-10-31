// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>How the player wakes up after a blackout.</summary>
public enum WakeUpPose
{
    Standing,
    OnGround,
    OnGround2,
}

/// <summary>
/// The player passing out (<c>BlackoutTrigger</c>: the screen fades, the player may be moved and wakes up
/// later), not a power cut. Scripted blackouts are in a few scenes (the casino, a story flat).
/// </summary>
public static class Blackouts
{
    private static readonly AccessTools.FieldRef<BlackoutTrigger, bool> Running = AccessTools.FieldRefAccess<BlackoutTrigger, bool>("running");

    /// <summary>A blackout sequence started. The payload is the trigger.</summary>
    public static readonly WorldEvent<BlackoutTrigger> Started = new WorldEvent<BlackoutTrigger>(nameof(Started));

    /// <summary>True while some blackout sequence runs. Scans the scene.</summary>
    public static bool IsRunning
    {
        get
        {
            foreach (var trigger in UnityEngine.Object.FindObjectsByType<BlackoutTrigger>(FindObjectsSortMode.None))
            {
                if (Running(trigger))
                    return true;
            }
            return false;
        }
    }

    internal static bool IsTriggerRunning(BlackoutTrigger trigger) => Running(trigger);

    /// <summary>
    /// Arranges the wake-up of the blackout that is coming or running: fade speed, real seconds until the
    /// player wakes, the pose, and whether it happens by itself. It applies once the level the blackout carries
    /// the player to has loaded, so it is allowed during that level change. False outside a gameplay scene.
    /// </summary>
    public static bool ScheduleWakeUp(float fadeOutSpeed, float wakeUpDelay, WakeUpPose pose, bool automatic)
    {
        var controller = BlackoutController.instance;
        if (controller == null)
            return false;
        controller.SetScheduleWakeUp(fadeOutSpeed, Math.Max(0f, wakeUpDelay), ToGame(pose), automatic);
        return true;
    }

    /// <summary>
    /// Wakes the player from a blackout now, with the pose and delay the blackout chose for a wake-up by hand
    /// (not those of <see cref="ScheduleWakeUp"/>). False outside a gameplay scene, or while a save is written or loaded.
    /// </summary>
    public static bool WakeUp()
    {
        var controller = BlackoutController.instance;
        if (controller == null || BlackCanvas.instance == null || SaveGate.IsBusy)
            return false;
        controller.TriggerWakeUp();
        return true;
    }

    private static BlackoutTrigger.WakeUpAnimationType ToGame(WakeUpPose pose) => pose switch
    {
        WakeUpPose.OnGround => BlackoutTrigger.WakeUpAnimationType.OnGround,
        WakeUpPose.OnGround2 => BlackoutTrigger.WakeUpAnimationType.OnGround2,
        _ => BlackoutTrigger.WakeUpAnimationType.None,
    };
}

[HarmonyPatch(typeof(BlackoutTrigger), nameof(BlackoutTrigger.StartBlackout))]
internal static class BlackoutPatch
{
    [HarmonyPrefix]
    private static void Before(BlackoutTrigger __instance, out bool __state) => __state = Blackouts.IsTriggerRunning(__instance);

    [HarmonyPostfix]
    private static void After(BlackoutTrigger __instance, bool __state)
    {
        try
        {
            if (!__state && Blackouts.Started.HasSubscribers && Blackouts.IsTriggerRunning(__instance))
                Blackouts.Started.Raise(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Blackout hook", e);
        }
    }
}
