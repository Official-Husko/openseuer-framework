namespace ObenFramework.Player;

/// <summary>
/// The kinds of wait the game knows besides sleeping. The kind only changes the text on the clock
/// ("Waiting", "Working", "Reading"...) and which NPC lines react to it. Values match the game's
/// <c>WaitingController.Type</c>.
/// </summary>
public enum WaitKind
{
    Waiting = 3,
    Working = 4,
    HavingFun = 5,
    Reading = 6,
    Playing = 7,
    StaringWall = 8,
    WatchingTV = 10,
    Talking = 11,
    Studying = 12,
}

/// <summary>How a mod's wait started with <see cref="PlayerSleep.Wait(ObenLoader.ObenMod, int, WaitKind, WaitOptions, System.Action{WaitResult})"/> behaves.</summary>
public sealed class WaitOptions
{
    /// <summary>
    /// Hunger, thirst, tiredness, hygiene and mood stay where they are for the length of the wait, and
    /// needs don't wake the player up. Time, crafting and the world go on as usual.
    /// </summary>
    public bool FreezeNeeds { get; set; }

    /// <summary>The player can't pass out from exhaustion or drink while the wait runs.</summary>
    public bool NoPassOut { get; set; }

    /// <summary>Text the clock shows instead of the kind's own ("Fixing the boiler"), or null.</summary>
    public string Label { get; set; }
}

/// <summary>How long a mod's wait lasted.</summary>
public readonly struct WaitResult
{
    public WaitResult(int seconds, bool interrupted)
    {
        Seconds = seconds;
        Interrupted = interrupted;
    }

    /// <summary>Game seconds that passed while waiting.</summary>
    public int Seconds { get; }

    /// <summary>Whole game minutes that passed.</summary>
    public int Minutes => WaitMath.MinutesPassed(Seconds);

    /// <summary>
    /// The wait didn't end on its own: the level changed, another wait replaced it or the framework
    /// unloaded. A wait the player cut short or that needs ended is not counted as interrupted.
    /// </summary>
    public bool Interrupted { get; }
}

/// <summary>What <see cref="PlayerSleep.SleepStarted"/> reports.</summary>
public readonly struct SleepStart
{
    public SleepStart(int alarmSeconds, bool passedOut)
    {
        AlarmSeconds = alarmSeconds;
        PassedOut = passedOut;
    }

    /// <summary>Game seconds until the alarm, or -1 without one.</summary>
    public int AlarmSeconds { get; }

    /// <summary>The player passed out (exhaustion, drink, an arrest) rather than going to bed.</summary>
    public bool PassedOut { get; }
}

/// <summary>What <see cref="PlayerSleep.WokeUp"/> reports.</summary>
public readonly struct RestEnd
{
    public RestEnd(WaitingController.Type kind, int seconds, bool alarm)
    {
        Kind = kind;
        Seconds = seconds;
        Alarm = alarm;
    }

    /// <summary>What the player was doing: Sleeping, Passout, Arrest or one of the wait kinds.</summary>
    public WaitingController.Type Kind { get; }

    /// <summary>Game seconds the sleep or wait lasted.</summary>
    public int Seconds { get; }

    /// <summary>An alarm clock ended the sleep.</summary>
    public bool Alarm { get; }

    public bool WasSleeping => Kind == WaitingController.Type.Sleeping || Kind == WaitingController.Type.Passout;
}
