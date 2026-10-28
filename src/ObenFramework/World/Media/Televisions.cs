// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Items;

namespace ObenFramework.World;

/// <summary>A television's state.</summary>
public readonly struct TelevisionInfo
{
    public TelevisionInfo(TelevisionNoise television, string id, bool on, bool powered, bool locked)
    {
        Television = television;
        Id = id;
        On = on;
        Powered = powered;
        Locked = locked;
    }

    public TelevisionNoise Television { get; }
    public string Id { get; }
    public bool On { get; }
    public bool Powered { get; }

    /// <summary>The level keeps it as it is (the player can't switch it).</summary>
    public bool Locked { get; }
}

/// <summary>Televisions (the game's noise TVs): switch them and hear when they change.</summary>
public static class Televisions
{
    private static readonly AccessTools.FieldRef<TelevisionNoise, bool> OnField = AccessTools.FieldRefAccess<TelevisionNoise, bool>("isOn");
    private static readonly AccessTools.FieldRef<TelevisionNoise, bool> PowerField = AccessTools.FieldRefAccess<TelevisionNoise, bool>("powerOn");
    private static readonly AccessTools.FieldRef<TelevisionNoise, bool> LockedField = AccessTools.FieldRefAccess<TelevisionNoise, bool>("locked");

    public static readonly WorldEvent<TelevisionInfo> Changed = new WorldEvent<TelevisionInfo>(nameof(Changed));

    public static List<TelevisionNoise> All() => ObjectIds.Active<TelevisionNoise>();

    public static TelevisionNoise Find(string id) => ObjectIds.Find<TelevisionNoise>(id);

    public static string IdOf(TelevisionNoise television) => ObjectIds.IdOf(television);

    public static TelevisionInfo Describe(TelevisionNoise television)
    {
        if (television == null)
            throw new ArgumentNullException(nameof(television));
        return new TelevisionInfo(television, IdOf(television), OnField(television), PowerField(television), LockedField(television));
    }

    /// <summary>Switches the TV on or off with its sound. False when it has no power (on) or the level locked it.</summary>
    public static bool SetOn(TelevisionNoise television, bool on)
    {
        if (television == null)
            throw new ArgumentNullException(nameof(television));
        if (LockedField(television) || (on && !PowerField(television)))
            return false;
        if (OnField(television) != on)
        {
            using (WorldChanges.EnterMod())
            {
                if (on)
                    television.TurnOn(true);
                else
                    television.TurnOff(true);
            }
        }
        return OnField(television) == on;
    }

    [HarmonyPatch]
    private static class TelevisionPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(TelevisionNoise), nameof(TelevisionNoise.TurnOn), new[] { typeof(bool), typeof(bool) });
            yield return AccessTools.Method(typeof(TelevisionNoise), nameof(TelevisionNoise.TurnOff), new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            yield return AccessTools.Method(typeof(TelevisionNoise), nameof(TelevisionNoise.PowerOff));
            yield return AccessTools.Method(typeof(TelevisionNoise), nameof(TelevisionNoise.PowerOn));
        }

        [HarmonyPrefix]
        private static void Before(bool ___isOn, bool ___powerOn, out int __state) => __state = (___isOn ? 1 : 0) | (___powerOn ? 2 : 0);

        [HarmonyPostfix]
        private static void After(TelevisionNoise __instance, bool ___isOn, bool ___powerOn, int __state)
        {
            try
            {
                if (__state != ((___isOn ? 1 : 0) | (___powerOn ? 2 : 0)) && Changed.HasSubscribers && WorldChanges.Active)
                    Changed.Raise(Describe(__instance));
            }
            catch (Exception e) { WorldLog.Failed("Television hook", e); }
        }
    }
}
