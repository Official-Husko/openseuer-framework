using NPC;

namespace ObenFramework.Npcs;

/// <summary>One entry of an NPC's schedule, copied from the game's <c>ScheduledAction</c>.</summary>
public sealed class NpcScheduleEntry
{
    /// <summary>The entry's name in the schedule database ("Sleep", "Work"...). Can be empty or shared.</summary>
    public string Name { get; }

    /// <summary>Position in the list it came from (timetable or random activities). Stable for a session.</summary>
    public int Index { get; }

    /// <summary>True for a random activity, false for a timetable entry.</summary>
    public bool Random { get; }

    /// <summary>The entry is switched off and the game skips it.</summary>
    public bool Disabled { get; }

    /// <summary>Day of the week it starts on, 0 to 6 (timetable entries only).</summary>
    public int WeekDay { get; }

    /// <summary>Hour of the day it starts, as a fraction: 13.5 is half past one in the afternoon.</summary>
    public float StartHour { get; }

    /// <summary>How long it lasts, in game minutes.</summary>
    public float DurationMinutes { get; }

    /// <summary>Chance, 0 to 1, that the game takes the entry when its time comes.</summary>
    public float Chance { get; }

    /// <summary>Whether the entry cuts off whatever the NPC is doing.</summary>
    public bool InterruptsPrevious { get; }

    /// <summary>The activity's name, or its class name ("Act_Stand", "Act_Sleep"...) when it has none.</summary>
    public string Activity { get; }

    /// <summary>The scene the activity takes place in, or null when it does not name one.</summary>
    public string Place { get; }

    internal NpcScheduleEntry(ScheduledAction action, int index, bool random)
    {
        Name = action.name ?? string.Empty;
        Index = index;
        Random = random;
        Disabled = action.disabled;
        WeekDay = action.starts != null ? action.starts.weekDay : 0;
        StartHour = action.starts != null ? action.starts.hours + action.starts.minutes / 60f : 0f;
        DurationMinutes = action.duration != null ? action.duration.TotalMinutesWithWeekDay : 0f;
        Chance = action.chance;
        InterruptsPrevious = action.interruptPrevious;
        Activity = NpcNames.Of(action.activity);
        Place = NpcNames.PlaceOf(action.activity);
    }
}
