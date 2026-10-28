using System;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Transport;

/// <summary>
/// Remembers which exit started a level change and who used it, so <see cref="Levels.Leaving"/> can name it.
/// A door to another level plays its animation first and changes the level after a delay, by which time the
/// player's interact scope has ended; the door's use is remembered for a short while instead.
/// </summary>
internal static class LevelWatch
{
    private const float DoorMemorySeconds = 15f;

    private static Changelevel _exit;
    private static DoorChangelevel _door;
    private static ChangeSource _doorSource;
    private static float _doorAt;
    private static ChangeSource _exitSource;

    public static void ExitUsed(Changelevel exit)
    {
        _exit = exit;
        _exitSource = WorldChanges.Current;
        if (exit != null && exit == _door && Time.realtimeSinceStartup - _doorAt < DoorMemorySeconds)
            _exitSource = _doorSource;
    }

    public static void DoorUsed(DoorChangelevel door)
    {
        _door = door;
        _doorSource = WorldChanges.Current;
        _doorAt = Time.realtimeSinceStartup;
    }

    /// <summary>The id of the exit that is changing the level right now, or null. Clears it.</summary>
    public static string TakeExit(out ChangeSource source)
    {
        source = _exitSource;
        var exit = _exit;
        _exit = null;
        return exit != null ? Levels.IdOf(exit) : null;
    }

    public static void Reset()
    {
        _exit = null;
        _door = null;
    }
}

[HarmonyPatch(typeof(Changelevel), nameof(Changelevel.ChangeLevel))]
internal static class ExitChangeLevelPatch
{
    [HarmonyPrefix]
    private static void Before(Changelevel __instance)
    {
        try { LevelWatch.ExitUsed(__instance); }
        catch (Exception e) { TransportModule.Log.Error("Level exit hook", e); }
    }
}

[HarmonyPatch(typeof(DoorChangelevel), nameof(DoorChangelevel.UseDoor))]
internal static class ExitDoorUsePatch
{
    [HarmonyPrefix]
    private static void Before(DoorChangelevel __instance)
    {
        try { LevelWatch.DoorUsed(__instance); }
        catch (Exception e) { TransportModule.Log.Error("Level door hook", e); }
    }
}

[HarmonyPatch(typeof(SaveController), nameof(SaveController.ChangeLevel))]
internal static class LevelChangePatch
{
    [HarmonyPrefix]
    private static void Before(string newlevel, string newLevelEntrypoint)
    {
        try { Levels.OnLeaving(newlevel, newLevelEntrypoint); }
        catch (Exception e) { TransportModule.Log.Error("Level change hook", e); }
    }
}
