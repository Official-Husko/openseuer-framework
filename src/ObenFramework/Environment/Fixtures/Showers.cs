// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A shower switched on or off, as raised by <see cref="Showers.ShowerChanged"/>.</summary>
public readonly struct ShowerChange
{
    public ShowerChange(InteractableShower shower, bool on, ChangeSource source)
    {
        Shower = shower;
        On = on;
        Source = source;
    }

    public InteractableShower Shower { get; }

    public bool On { get; }

    public ChangeSource Source { get; }

    public string Id => Showers.IdOf(Shower);
}

/// <summary>A snapshot of one shower.</summary>
public sealed class ShowerInfo
{
    internal ShowerInfo(string id, string name, bool on, bool running)
    {
        Id = id;
        Name = name;
        On = on;
        Running = running;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>Its tap is open.</summary>
    public bool On { get; }

    /// <summary>Water actually comes out (the supply has water).</summary>
    public bool Running { get; }
}

/// <summary>Showers: turn them on and off; standing under a running one washes the player. Saved with the game.</summary>
public static class Showers
{
    private static readonly AccessTools.FieldRef<InteractableShower, bool> ShowerOn = AccessTools.FieldRefAccess<InteractableShower, bool>("showerOn");
    private static readonly AccessTools.FieldRef<InteractableShower, bool> WaterOn = AccessTools.FieldRefAccess<InteractableShower, bool>("waterOn");

    /// <summary>A shower was turned on or off. Not raised while a save loads.</summary>
    public static readonly WorldEvent<ShowerChange> ShowerChanged = new WorldEvent<ShowerChange>(nameof(ShowerChanged));

    public static List<ShowerInfo> All()
    {
        var list = new List<ShowerInfo>();
        foreach (var shower in ObjectIds.Active<InteractableShower>())
            list.Add(Describe(shower));
        return list;
    }

    public static InteractableShower Find(string id) => ObjectIds.Find<InteractableShower>(id);

    public static string IdOf(InteractableShower shower) => ObjectIds.IdOf(shower);

    public static bool IsOn(InteractableShower shower) => ShowerOn(shower);

    public static ShowerInfo Describe(InteractableShower shower) => new ShowerInfo(IdOf(shower), shower.name, ShowerOn(shower), WaterOn(shower));

    /// <summary>Turns the shower on or off, with its sound.</summary>
    public static void SetOn(InteractableShower shower, bool on)
    {
        if (shower == null)
            throw new ArgumentNullException(nameof(shower));
        if (IsOn(shower) == on)
            return;
        using (WorldChanges.EnterMod())
        {
            if (on)
                shower.TurnOn(true, false);
            else
                shower.TurnOff(true);
        }
    }
}
