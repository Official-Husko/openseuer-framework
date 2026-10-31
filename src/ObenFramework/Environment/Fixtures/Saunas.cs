// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A sauna stove switched on or off, as raised by <see cref="Saunas.SaunaChanged"/>.</summary>
public readonly struct SaunaChange
{
    public SaunaChange(Sauna sauna, bool on, float temperature, float steam, ChangeSource source)
    {
        Sauna = sauna;
        On = on;
        Temperature = temperature;
        Steam = steam;
        Source = source;
    }

    public Sauna Sauna { get; }

    public bool On { get; }

    /// <summary>The room's temperature when it was switched, in degrees Celsius.</summary>
    public float Temperature { get; }

    /// <summary>The löyly when it was switched (a passive sauna's steam follows its stove).</summary>
    public float Steam { get; }

    public ChangeSource Source { get; }

    public string Id => Saunas.IdOf(Sauna);
}

/// <summary>A snapshot of one sauna.</summary>
public sealed class SaunaInfo
{
    internal SaunaInfo(string id, string name, bool on, float temperature, float steam, bool passive)
    {
        Id = id;
        Name = name;
        On = on;
        Temperature = temperature;
        Steam = steam;
        Passive = passive;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>The stove is on (it heats up towards about 100 degrees).</summary>
    public bool On { get; }

    /// <summary>The room's temperature in degrees Celsius.</summary>
    public float Temperature { get; }

    /// <summary>The löyly: steam from water thrown on the stones, which fades over time.</summary>
    public float Steam { get; }

    /// <summary>A passive sauna has steady steam and no water throwing.</summary>
    public bool Passive { get; }
}

/// <summary>
/// Saunas: switch the stove, throw water, and read the heat and steam. Sitting in a hot sauna cleans the
/// player and eases drunkenness, addictions and depression. Saved with the game. Changes are refused (false)
/// while a save is written or loaded.
/// </summary>
public static class Saunas
{
    private static readonly AccessTools.FieldRef<Sauna, bool> CanThrow = AccessTools.FieldRefAccess<Sauna, bool>("canThrowWater");

    /// <summary>A sauna stove was switched. Not raised while a save loads.</summary>
    public static readonly WorldEvent<SaunaChange> SaunaChanged = new WorldEvent<SaunaChange>(nameof(SaunaChanged));

    /// <summary>True when the hooks are in place, so <see cref="SaunaChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    public static List<SaunaInfo> All()
    {
        var list = new List<SaunaInfo>();
        foreach (var sauna in ObjectIds.Active<Sauna>())
            list.Add(Describe(sauna));
        return list;
    }

    public static Sauna Find(string id) => ObjectIds.Find<Sauna>(id);

    public static string IdOf(Sauna sauna) => ObjectIds.IdOf(sauna);

    public static SaunaInfo Describe(Sauna sauna) => new SaunaInfo(IdOf(sauna), sauna.name, sauna.On, sauna.temperature, sauna.löyly, sauna.passive);

    /// <summary>Switches the stove. False while a save is written or loaded.</summary>
    public static bool SetOn(Sauna sauna, bool on)
    {
        if (sauna == null)
            throw new ArgumentNullException(nameof(sauna));
        if (sauna.On == on)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (on)
                sauna.TurnOn();
            else
                sauna.TurnOff();
        }
        return sauna.On == on;
    }

    /// <summary>
    /// Throws a ladle of water on the stones, like the player. False for a passive sauna, while the last throw
    /// is still settling (a second and a half), or while a save is written or loaded.
    /// </summary>
    public static bool ThrowWater(Sauna sauna)
    {
        if (sauna == null)
            throw new ArgumentNullException(nameof(sauna));
        if (sauna.passive || !CanThrow(sauna) || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            sauna.ThrowWater();
        return true;
    }
}
