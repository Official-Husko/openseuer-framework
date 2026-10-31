// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>What happened to a lantern, as raised by <see cref="Lanterns.LanternChanged"/>.</summary>
public enum LanternChangeKind
{
    /// <summary>A candle went in and was lit.</summary>
    Lit,

    /// <summary>Someone blew the candle out before it burnt down.</summary>
    Extinguished,

    /// <summary>The candle burnt down.</summary>
    BurntOut,
}

public readonly struct LanternChange
{
    public LanternChange(Lantern lantern, LanternChangeKind kind, ChangeSource source)
    {
        Lantern = lantern;
        Kind = kind;
        Source = source;
    }

    public Lantern Lantern { get; }

    public LanternChangeKind Kind { get; }

    public ChangeSource Source { get; }

    public string Id => Lanterns.IdOf(Lantern);
}

/// <summary>A snapshot of one candle lantern.</summary>
public sealed class LanternInfo
{
    internal LanternInfo(string id, string name, Vector3 position, bool lit, int secondsLeft)
    {
        Id = id;
        Name = name;
        Position = position;
        Lit = lit;
        SecondsLeft = secondsLeft;
    }

    public string Id { get; }

    public string Name { get; }

    public Vector3 Position { get; }

    public bool Lit { get; }

    /// <summary>Game seconds until the candle burns down; 0 when unlit.</summary>
    public int SecondsLeft { get; }
}

/// <summary>
/// Candle lanterns: a candle goes in, burns for a while on the game clock and goes out. Saved with the game.
/// Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class Lanterns
{
    /// <summary>A lantern was lit, blown out or burnt down. Not raised while a save loads.</summary>
    public static readonly WorldEvent<LanternChange> LanternChanged = new WorldEvent<LanternChange>(nameof(LanternChanged));

    /// <summary>True when the hooks are in place, so <see cref="LanternChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    public static List<LanternInfo> All()
    {
        var list = new List<LanternInfo>();
        foreach (var lantern in ObjectIds.Active<Lantern>())
            list.Add(Describe(lantern));
        return list;
    }

    public static Lantern Find(string id) => ObjectIds.Find<Lantern>(id);

    public static string IdOf(Lantern lantern) => ObjectIds.IdOf(lantern);

    public static bool IsLit(Lantern lantern) => lantern.candleTimer != null && lantern.candleTimer.remainingInSeconds > 0;

    public static LanternInfo Describe(Lantern lantern)
    {
        bool lit = IsLit(lantern);
        return new LanternInfo(IdOf(lantern), lantern.name, lantern.transform.position, lit, lit ? lantern.candleTimer.remainingInSeconds : 0);
    }

    /// <summary>
    /// Puts a candle in and lights it, without taking one from the player. True when it is lit afterwards (also
    /// when it already was); false for a lantern that names no candle item.
    /// </summary>
    public static bool Light(Lantern lantern)
    {
        if (lantern == null)
            throw new ArgumentNullException(nameof(lantern));
        if (IsLit(lantern))
            return true;
        // Without a fixed burn time the game reads it from the candle item.
        if (SaveGate.IsBusy || (lantern.overrideCandleTime < 0 && (lantern.candleItem == null || lantern.candleItem.Item == null)))
            return false;
        using (WorldChanges.EnterMod())
            lantern.CandleInserted();
        return IsLit(lantern);
    }

    /// <summary>
    /// Blows the candle out (the rest of it is lost, as when the player does it). True when it is out afterwards.
    /// Unlike the game's own blowing out, the candle's timer is stopped too, so it can't put out the next candle.
    /// </summary>
    public static bool Extinguish(Lantern lantern)
    {
        if (lantern == null)
            throw new ArgumentNullException(nameof(lantern));
        if (!IsLit(lantern))
            return true;
        if (SaveGate.IsBusy)
            return false;
        var timer = lantern.candleTimer;
        using (WorldChanges.EnterMod())
            lantern.CandleExtinguished();
        timer?.Destroy();
        return !IsLit(lantern);
    }
}
