// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Patching;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Clock;

/// <summary>
/// Freezing the in-game clock and changing its speed, held per mod. Any mod's freeze stops time until
/// every freezing mod lets go; of the speeds the newest wins, and the one before applies again when its
/// mod clears it. Everything a mod holds goes when it unloads, and all overrides end at the main menu.
/// Unlike <see cref="GameClock.TimeSpeed"/> this doesn't touch the game's own speed factor: sleeping,
/// waiting and crafting waits still run time fast (the overrides stand aside meanwhile) and the
/// overrides apply again afterwards.
/// </summary>
public static class TimeControl
{
    internal static readonly TimeOverrides Overrides = new TimeOverrides();

    private static readonly HashSet<ITracker> Tracked = new HashSet<ITracker>();

    /// <summary>The clock hook is in place (its patch didn't fail). Without it the overrides do nothing.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Clock");

    /// <summary>Some mod is freezing time.</summary>
    public static bool IsFrozen => Overrides.IsFrozen;

    /// <summary>The speed mods asked for (newest wins), 1 when none. 2 runs time twice as fast.</summary>
    public static float Speed => Overrides.Speed;

    /// <summary>
    /// The overrides are standing aside right now because the game itself runs time fast (sleeping,
    /// waiting, a crafting wait).
    /// </summary>
    public static bool IsSuspended => Overrides.Any && GameFactor() > 1f;

    /// <summary>Stops in-game time until <paramref name="owner"/> calls <see cref="Unfreeze"/> or unloads.</summary>
    public static void Freeze(ITracker owner)
    {
        Track(owner);
        if (Overrides.Freeze(owner))
            GameEvents.Log?.Info($"Time frozen by {NameOf(owner)}");
    }

    /// <summary>Lets go of <paramref name="owner"/>'s freeze. Time runs again once no mod freezes it.</summary>
    public static void Unfreeze(ITracker owner)
    {
        if (owner != null && Overrides.Unfreeze(owner))
            GameEvents.Log?.Info($"Time unfrozen by {NameOf(owner)}{(Overrides.IsFrozen ? ", still frozen by another mod" : "")}");
    }

    public static bool IsFrozenBy(ITracker owner) => owner != null && Overrides.IsFrozenBy(owner);

    /// <summary>
    /// Runs in-game time <paramref name="speed"/> times as fast (0.01 to 100; 1 is normal) until
    /// <paramref name="owner"/> clears it or unloads. Returns the speed after clamping.
    /// </summary>
    public static float SetSpeed(ITracker owner, float speed)
    {
        Track(owner);
        return Overrides.SetSpeed(owner, speed);
    }

    /// <summary>Drops <paramref name="owner"/>'s speed; the one set before it (or normal speed) applies again.</summary>
    public static void ClearSpeed(ITracker owner)
    {
        if (owner != null)
            Overrides.ClearSpeed(owner);
    }

    /// <summary>Who holds what, for tools: "freeze" or the speed, by owner name, oldest first.</summary>
    public static List<KeyValuePair<string, string>> Holders()
    {
        var list = new List<KeyValuePair<string, string>>();
        foreach (var owner in Overrides.Freezers)
            list.Add(new KeyValuePair<string, string>(NameOf(owner), "freeze"));
        foreach (var pair in Overrides.Speeds)
            list.Add(new KeyValuePair<string, string>(NameOf(pair.Key), "speed " + pair.Value.ToString("0.###")));
        return list;
    }

    internal static void Init(ObenMod framework)
    {
        Overrides.Clear();
        Tracked.Clear();
        GameEvents.MainMenu.Subscribe(framework, ReleaseAll);
        framework.Track(Disposable.Create(() =>
        {
            Overrides.Clear();
            Tracked.Clear();
        }));
    }

    /// <summary>The day length for the time controller (called from the clock hook every frame while an override is held).</summary>
    internal static float AdjustDayLength(float requested) => Overrides.DayLength(requested, GameFactor());

    private static float GameFactor()
    {
        var azure = TimeOfDayAzure.instance;
        return azure != null ? azure.timeSpeedOverrideFactor : 1f;
    }

    private static void ReleaseAll()
    {
        if (Overrides.Any)
            GameEvents.Log?.Info("Back at the main menu: clock overrides cleared");
        Overrides.Clear();
    }

    // One cleanup per owner, however often it freezes and lets go.
    private static void Track(ITracker owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (!Tracked.Add(owner))
            return;
        owner.Track(Disposable.Create(() =>
        {
            Tracked.Remove(owner);
            Overrides.Release(owner);
        }));
    }

    private static string NameOf(object owner) =>
        owner is ObenMod mod ? mod.Info.Id : owner?.GetType().Name ?? "?";
}
