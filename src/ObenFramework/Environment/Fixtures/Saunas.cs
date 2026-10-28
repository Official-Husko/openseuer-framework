// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A sauna stove switched on or off, as raised by <see cref="Saunas.SaunaChanged"/>.</summary>
public readonly struct SaunaChange
{
    public SaunaChange(Sauna sauna, bool on, ChangeSource source)
    {
        Sauna = sauna;
        On = on;
        Source = source;
    }

    public Sauna Sauna { get; }

    public bool On { get; }

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
/// player and eases drunkenness, addictions and depression. Saved with the game.
/// </summary>
public static class Saunas
{
    /// <summary>A sauna stove was switched. Not raised while a save loads.</summary>
    public static readonly WorldEvent<SaunaChange> SaunaChanged = new WorldEvent<SaunaChange>(nameof(SaunaChanged));

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

    public static void SetOn(Sauna sauna, bool on)
    {
        if (sauna == null)
            throw new ArgumentNullException(nameof(sauna));
        if (sauna.On == on)
            return;
        using (WorldChanges.EnterMod())
        {
            if (on)
                sauna.TurnOn();
            else
                sauna.TurnOff();
        }
    }

    /// <summary>Throws a ladle of water on the stones, like the player. Passive saunas and a ladle still cooling down ignore it.</summary>
    public static void ThrowWater(Sauna sauna)
    {
        if (sauna == null)
            throw new ArgumentNullException(nameof(sauna));
        using (WorldChanges.EnterMod())
            sauna.ThrowWater();
    }
}
