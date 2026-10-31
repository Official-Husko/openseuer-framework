using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World;

public enum LightChangeKind
{
    /// <summary>The fixture's switch went on (it may still have no power).</summary>
    TurnedOn,

    TurnedOff,

    /// <summary>The fixture got power back.</summary>
    PowerRestored,

    /// <summary>The fixture lost power (the power grid, a breaker or a script).</summary>
    PowerLost,
}

/// <summary>A change of one light fixture, as raised by <see cref="Lights.LightChanged"/>.</summary>
public readonly struct LightChange
{
    public LightChange(LightTurnOffOn light, LightChangeKind kind, ChangeSource source)
    {
        Light = light;
        Kind = kind;
        Source = source;
    }

    public LightTurnOffOn Light { get; }

    public LightChangeKind Kind { get; }

    public ChangeSource Source { get; }

    /// <summary>The fixture is lit after the change: switched on and powered.</summary>
    public bool IsLit => Light != null && Lights.IsLit(Light);

    public string Id => Lights.IdOf(Light);
}

/// <summary>
/// Light fixtures (<c>LightTurnOffOn</c>, the ceiling and wall lights that wall switches and the power
/// grid drive). A fixture shines when its switch is on and it has power. Fixtures with several
/// light states (<c>LightTurnOffOnMultiState</c>) are <c>ObenFramework.Env.MultiStateLights</c>; the old
/// <c>LightsON</c> switches are not covered. Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class Lights
{
    /// <summary>
    /// A fixture was switched or lost/regained power. Only real changes are raised, and not while a save
    /// loads. Lights that follow the time of day raise a burst at dusk and dawn.
    /// </summary>
    public static readonly WorldEvent<LightChange> LightChanged = new WorldEvent<LightChange>(nameof(LightChanged));

    /// <summary>True when the hooks are in place, so <see cref="LightChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    public static List<LightTurnOffOn> All(bool includeInactive = false) =>
        new List<LightTurnOffOn>(Object.FindObjectsByType<LightTurnOffOn>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None));

    public static LightTurnOffOn Find(string id) => ObjectResolver.Find<LightTurnOffOn>(id);

    /// <summary>GUID where the editor set one, else the full address text.</summary>
    public static string IdOf(LightTurnOffOn light)
    {
        if (light == null)
            return null;
        return string.IsNullOrEmpty(light.GUID) ? ObjectAddress.Of(light).ToString() : light.GUID;
    }

    /// <summary>The fixture's own switch.</summary>
    public static bool IsOn(LightTurnOffOn light) => light.on;

    /// <summary>The fixture has power (it may still be switched off).</summary>
    public static bool IsPowered(LightTurnOffOn light) => !light.noPower;

    /// <summary>Switched on and powered: what the player sees.</summary>
    public static bool IsLit(LightTurnOffOn light) => light.on && !light.noPower;

    /// <summary>
    /// Flips the fixture's switch the way a wall switch does (<c>turnLightOn</c> / <c>turnLightOff</c>). A
    /// fixture that is fading out (a timed or scripted switch-off) is faded back in, or it would go dark again.
    /// </summary>
    public static bool SetOn(LightTurnOffOn light, bool on)
    {
        if (light == null)
            throw new ArgumentNullException(nameof(light));
        if (light.on == on)
            return true;
        if (SaveGate.IsBusy)
            return false;

        using (WorldChanges.EnterMod())
        {
            if (on && FadingOut(light))
                light.TurnLightOnFade(false);
            else if (on)
                light.turnLightOn();
            else
                light.turnLightOff();
        }
        return light.on == on;
    }

    private static readonly AccessTools.FieldRef<LightTurnOffOn, bool> FadeOut = AccessTools.FieldRefAccess<LightTurnOffOn, bool>("fadeOut");

    private static bool FadingOut(LightTurnOffOn light) => FadeOut(light);

    /// <summary>Gives or takes the fixture's power the way the power grid does (<c>TogglePower</c>).</summary>
    public static bool SetPowered(LightTurnOffOn light, bool powered)
    {
        if (light == null)
            throw new ArgumentNullException(nameof(light));
        if (IsPowered(light) == powered)
            return true;
        if (SaveGate.IsBusy)
            return false;

        using (WorldChanges.EnterMod())
            light.TogglePower(powered);
        return IsPowered(light) == powered;
    }
}
