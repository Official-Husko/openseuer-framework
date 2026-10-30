// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;

namespace ObenFramework.World;

/// <summary>A radio's state.</summary>
public readonly struct RadioInfo
{
    public RadioInfo(Radio radio, string id, bool enabled, int station, int stations)
    {
        Radio = radio;
        Id = id;
        Enabled = enabled;
        Station = station;
        Stations = stations;
    }

    public Radio Radio { get; }
    public string Id { get; }

    /// <summary>Has power (an unpowered radio stays silent whatever its station).</summary>
    public bool Enabled { get; }

    /// <summary>0 is off, 1 and up the stations it cycles through.</summary>
    public int Station { get; }

    /// <summary>How many settings it has, off included.</summary>
    public int Stations { get; }

    public bool Playing => Enabled && Station > 0;
}

/// <summary>
/// Radios: turn them on and off, tune them, cut their power, and hear when they change. The game saves their
/// state with the level.
/// </summary>
public static class Radios
{
    private static readonly AccessTools.FieldRef<Radio, int> StatesField = AccessTools.FieldRefAccess<Radio, int>("states");

    /// <summary>False when the framework's radio hooks failed to apply after a game update; <see cref="Changed"/> then stays quiet.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    /// <summary>A radio was tuned, switched on or off, or lost or got power.</summary>
    public static readonly WorldEvent<RadioInfo> Changed = new WorldEvent<RadioInfo>(nameof(Changed));

    public static List<Radio> All() => ObjectIds.Active<Radio>();

    public static Radio Find(string id) => ObjectIds.Find<Radio>(id);

    public static string IdOf(Radio radio) => ObjectIds.IdOf(radio);

    public static RadioInfo Describe(Radio radio)
    {
        if (radio == null)
            throw new ArgumentNullException(nameof(radio));
        return new RadioInfo(radio, IdOf(radio), radio.RadioEnabled, radio.CurrentState, StatesField(radio));
    }

    /// <summary>Tunes to a station (1 and up) or switches off (0), with the radio's own sounds.</summary>
    public static void SetStation(Radio radio, int station)
    {
        if (radio == null)
            throw new ArgumentNullException(nameof(radio));
        if (station < 0 || station >= StatesField(radio))
            throw new ArgumentOutOfRangeException(nameof(station), $"The radio has stations 0 to {StatesField(radio) - 1}.");
        using (WorldChanges.EnterMod())
            radio.SetState(station);
    }

    /// <summary>Gives the radio power or cuts it (it keeps its station).</summary>
    public static void SetEnabled(Radio radio, bool enabled)
    {
        if (radio == null)
            throw new ArgumentNullException(nameof(radio));
        using (WorldChanges.EnterMod())
            radio.EnableRadio(enabled);
    }

    [HarmonyPatch]
    private static class RadioPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<System.Reflection.MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(Radio), nameof(Radio.SetState));
            yield return AccessTools.Method(typeof(Radio), nameof(Radio.EnableRadio));
            yield return AccessTools.Method(typeof(Radio), nameof(Radio.TurnOff));
        }

        [HarmonyPrefix]
        private static void Before(Radio __instance, out long __state) => __state = Pack(__instance);

        [HarmonyPostfix]
        private static void After(Radio __instance, long __state)
        {
            try
            {
                if (__state != Pack(__instance) && Changed.HasSubscribers && WorldChanges.Active)
                    Changed.Raise(Describe(__instance));
            }
            catch (Exception e) { WorldLog.Failed("Radio hook", e); }
        }

        private static long Pack(Radio radio) => ((long)radio.CurrentState << 1) | (radio.RadioEnabled ? 1L : 0L);
    }
}
