// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;

namespace ObenFramework.Player;

/// <summary>
/// Sleeping and waiting: start either, wake the player up, and hear when they start and end. A mod's own
/// wait can freeze needs, block passing out, show its own label on the clock and report how long it
/// lasted. All of it goes through the game's own <c>WaitingController</c>, so time speeds up, sleep
/// events roll and NPCs react exactly as for a bed or a bench.
/// </summary>
public static class PlayerSleep
{
    /// <summary>The player fell asleep or passed out.</summary>
    public static readonly GameEvent<SleepStart> SleepStarted = new GameEvent<SleepStart>(nameof(SleepStarted));

    /// <summary>A wait started (on a bench, at work, reading, or one a mod started).</summary>
    public static readonly GameEvent<WaitingController.Type> WaitStarted = new GameEvent<WaitingController.Type>(nameof(WaitStarted));

    /// <summary>A sleep or wait ended and the player can move again.</summary>
    public static readonly GameEvent<RestEnd> WokeUp = new GameEvent<RestEnd>(nameof(WokeUp));

    /// <summary>Asleep or passed out.</summary>
    public static bool IsSleeping => WaitingController.instance != null && WaitingController.instance.IsSleeping;

    /// <summary>In a wait of any kind (not sleeping).</summary>
    public static bool IsWaiting => WaitingController.instance != null && WaitingController.instance.IsWaiting;

    /// <summary>Sleeping, passed out or waiting.</summary>
    public static bool IsResting => IsSleeping || IsWaiting;

    /// <summary>What the player is doing right now, <c>None</c> when awake.</summary>
    public static WaitingController.Type Current =>
        WaitingController.instance != null ? WaitingController.instance.waitingType : WaitingController.Type.None;

    /// <summary>Game seconds the current sleep or wait has lasted so far.</summary>
    public static int SecondsRested => WaitingController.instance != null ? WaitingController.instance.sleepingTimeSeconds : 0;

    /// <summary>A mod's wait from <see cref="Wait(ObenMod, int, WaitKind, WaitOptions, Action{WaitResult})"/> is running.</summary>
    public static bool ModWaitRunning => ModWait.Active;

    /// <summary>Whether a sleep or wait could start now; <paramref name="reason"/> says why not.</summary>
    public static bool CanRest(out string reason)
    {
        var waiting = WaitingController.instance;
        if (waiting == null || PlayerStats.instance == null || GameController.instance == null ||
            GameUIController.instance == null || TimeOfDayAzure.instance == null)
            reason = "There is no player right now (main menu or loading).";
        else if (SaveGate.IsBusy || !waiting.CanStartWaitingStaus)
            reason = "The game is saving, loading or changing level.";
        else if (WaitingController.WaitingBlocked)
            reason = "Something in the level blocks waiting right now.";
        else if (waiting.IsSleeping || waiting.IsWaiting)
            reason = "The player is already sleeping or waiting.";
        else if (TimePicker.IsOpen)
            reason = "A time picker is open.";
        else
            reason = null;
        return reason == null;
    }

    /// <summary>
    /// Puts the player to sleep where they stand, like a bed without its quality bonus. False when the
    /// player can't rest now or isn't tired enough (the game refuses sleep below a level of tiredness).
    /// </summary>
    /// <param name="alarmSeconds">Game seconds until the alarm wakes them, or -1 to sleep until rested.</param>
    public static bool Sleep(int alarmSeconds = -1)
    {
        if (alarmSeconds == 0 || alarmSeconds < -1)
            throw new ArgumentOutOfRangeException(nameof(alarmSeconds), "Give game seconds, or -1 for no alarm.");
        if (!CanRest(out _))
            return false;

        var waiting = WaitingController.instance;
        bool alarm = waiting.alarmEnabled;
        bool started = waiting.Sleep(alarmSeconds);
        if (!started)
            waiting.alarmEnabled = alarm; // Sleep switches the alarm on before it checks tiredness.
        return started;
    }

    /// <summary>Starts a plain wait of the given kind, like sitting on a bench. False when the player can't rest now.</summary>
    public static bool Wait(int seconds, WaitKind kind = WaitKind.Waiting)
    {
        CheckWait(seconds, kind);
        if (!CanRest(out _))
            return false;
        ModWait.FinishInterrupted();
        WaitingController.instance.Wait(seconds, (WaitingController.Type)kind);
        return true;
    }

    /// <summary>
    /// Starts a wait for a mod: <paramref name="onDone"/> is called once with how long it lasted, whether
    /// it ran out, the player cut it short or it was interrupted. Only one mod wait runs at a time.
    /// </summary>
    public static bool Wait(ObenMod owner, int seconds, WaitKind kind, WaitOptions options, Action<WaitResult> onDone)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (onDone == null)
            throw new ArgumentNullException(nameof(onDone));
        CheckWait(seconds, kind);
        if (!CanRest(out _))
            return false;
        ModWait.Start(owner, seconds, kind, options ?? new WaitOptions(), onDone, WaitingController.instance);
        return true;
    }

    /// <summary>
    /// Ends a sleep or wait as if the player woke up. False when they aren't resting or a sleep event
    /// (a theft, an arrest) is playing out.
    /// </summary>
    public static bool WakeUp()
    {
        var waiting = WaitingController.instance;
        if (waiting == null || !(waiting.IsSleeping || waiting.IsWaiting))
            return false;
        if (SleepEventController.instance != null && SleepEventController.instance.eventInProgress)
            return false;
        waiting.Wakeup();
        return !waiting.IsSleeping && !waiting.IsWaiting;
    }

    private static void CheckWait(int seconds, WaitKind kind)
    {
        if (seconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "A wait needs a positive number of game seconds.");
        if (!Enum.IsDefined(typeof(WaitKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
    }
}
