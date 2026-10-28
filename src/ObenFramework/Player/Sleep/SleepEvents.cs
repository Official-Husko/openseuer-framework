// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenLoader;

namespace ObenFramework.Player;

/// <summary>The game is picking what happens during a sleep. Handlers can look and cancel.</summary>
public sealed class SleepEventChoice
{
    internal SleepEventChoice(IReadOnlyList<SleepEventController.SleepEvents> candidates, SleepEventController.SleepEvents chosen)
    {
        Candidates = candidates;
        Chosen = chosen;
    }

    /// <summary>Everything that could have happened at this bed or spot (theft, beating, nightmare...).</summary>
    public IReadOnlyList<SleepEventController.SleepEvents> Candidates { get; }

    /// <summary>What the dice picked; <c>None</c> when nothing will happen (or only a scripted event will).</summary>
    public SleepEventController.SleepEvents Chosen { get; }

    public bool Cancelled { get; private set; }

    /// <summary>Nothing happens during this sleep. An arrest for open crimes is rolled separately and still can.</summary>
    public void Cancel() => Cancelled = true;
}

/// <summary>
/// The bad things that can happen while the player sleeps: theft, a beating, an arrest, junk dumped on
/// them, a nightmare. When a sleep starts the game rolls one from the bed's list (scaled by how safe the
/// spot is) and plays it out some hours later. This sees the roll, can cancel it, and reports what played.
/// </summary>
public static class SleepEvents
{
    private static readonly HandlerList<Action<SleepEventChoice>> Handlers = new HandlerList<Action<SleepEventChoice>>();

    private static readonly AccessTools.FieldRef<WaitingController, SleepEvent> Coming =
        AccessTools.FieldRefAccess<WaitingController, SleepEvent>("comingSleepEvent");

    /// <summary>A sleep event played out (the payload is which). Not raised for <c>None</c>.</summary>
    public static readonly GameEvent<SleepEventController.SleepEvents> Occurred =
        new GameEvent<SleepEventController.SleepEvents>(nameof(Occurred));

    /// <summary>
    /// Sees every roll for a sleep event and can cancel it. Lasts as long as <paramref name="owner"/> is loaded
    /// or until the result is disposed. Handlers run in order, each in its own try/catch; a cancel sticks.
    /// </summary>
    public static IDisposable Intercept(ObenMod owner, Action<SleepEventChoice> handler)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        return owner.Track(Handlers.Add(handler));
    }

    /// <summary>A sleep event is playing out right now (the player can't be woken).</summary>
    public static bool InProgress => SleepEventController.instance != null && SleepEventController.instance.eventInProgress;

    /// <summary>The player is in a spot marked safe (their own flat with the door locked and the like).</summary>
    public static bool PlayerInSafeArea => SleepEventController.instance != null && SleepEventController.instance.playerInSafeArea;

    /// <summary>
    /// How safe the current spot is, 0 to 1. An event's chance is its risk times (1 - safety), so 1 means
    /// nothing happens. 0 at the main menu.
    /// </summary>
    public static float SafetyFactor
    {
        get
        {
            var controller = SleepEventController.instance;
            if (controller == null)
                return 0f;
            if (controller.playerInSafeArea)
                return 1f;
            return info_game_logic.instance != null ? controller.playerSafetyFactor : 0f;
        }
    }

    /// <summary>The event waiting to happen in the current sleep, or <c>None</c>.</summary>
    public static SleepEventController.SleepEvents Planned
    {
        get
        {
            var waiting = WaitingController.instance;
            var coming = waiting != null ? Coming(waiting) : null;
            return coming != null ? coming.sleepEvent : SleepEventController.SleepEvents.None;
        }
    }

    /// <summary>Game seconds until <see cref="Planned"/> happens, or -1.</summary>
    public static int SecondsUntilPlanned => WaitingController.instance != null ? WaitingController.instance.timeToEventInSeconds : -1;

    internal static bool HasHandlers => Handlers.Count > 0;

    /// <summary>Runs the handlers; true when one of them cancelled.</summary>
    internal static bool Cancelled(SleepEvent[] candidates, SleepEvent chosen)
    {
        var kinds = new List<SleepEventController.SleepEvents>();
        if (candidates != null)
        {
            foreach (var candidate in candidates)
            {
                if (candidate != null)
                    kinds.Add(candidate.sleepEvent);
            }
        }

        var choice = new SleepEventChoice(kinds, chosen != null ? chosen.sleepEvent : SleepEventController.SleepEvents.None);
        Handlers.ForEach(h => h(choice), GameEvents.Log, "SleepEvents.Intercept");
        return choice.Cancelled;
    }
}
