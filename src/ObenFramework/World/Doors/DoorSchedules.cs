// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Clock;
using ObenFramework.Items;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// Opening hours of shops and other places whose doors lock and unlock on the game's clock (the game's
/// <c>DoorSchedule</c>). Read them, change them for as long as a mod is loaded, and find where characters
/// stand to use a door.
/// </summary>
public static class DoorSchedules
{
    private static readonly AccessTools.FieldRef<DoorSchedule, Vector2Int> LastCheckField =
        AccessTools.FieldRefAccess<DoorSchedule, Vector2Int>("lastCheck");

    /// <summary>The schedules of the loaded scenes. A scan.</summary>
    public static List<DoorSchedule> All() => ObjectIds.Active<DoorSchedule>();

    /// <summary>The schedule that locks and unlocks a door, or null when the door has none. A scan.</summary>
    public static DoorSchedule Of(Door door)
    {
        if (door == null)
            return null;
        foreach (var schedule in All())
        {
            if (schedule.Doors != null && Array.IndexOf(schedule.Doors, door) >= 0)
                return schedule;
        }
        return null;
    }

    public static DoorSchedule Find(string id) => ObjectIds.Find<DoorSchedule>(id);

    public static string IdOf(DoorSchedule schedule) => ObjectIds.IdOf(schedule);

    /// <summary>The hours a schedule keeps its doors open. Empty means always locked.</summary>
    public static List<OpeningHours> HoursOf(DoorSchedule schedule)
    {
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));
        return DoorScheduleRules.FromGame(Table(schedule.OpenTimes));
    }

    /// <summary>Whether the schedule has its doors open at the game's current day and hour.</summary>
    public static bool IsOpenNow(DoorSchedule schedule)
    {
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));
        return DoorScheduleRules.IsOpen(Table(schedule.OpenTimes), GameClock.WeekdayIndex, GameClock.Hour);
    }

    /// <summary>
    /// Replaces a schedule's hours until <paramref name="owner"/> unloads or the returned object is disposed, then
    /// puts the level's own hours back. The doors follow at the next game minute. The change isn't saved: the
    /// level's hours return when it loads again, so set them from a world-ready handler to keep them.
    /// </summary>
    public static IDisposable SetHours(ITracker owner, DoorSchedule schedule, IList<OpeningHours> hours)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));
        var table = DoorScheduleRules.ToGame(hours);

        var original = schedule.OpenTimes;
        var replaced = new Vector3Int[table.Count];
        for (int i = 0; i < table.Count; i++)
            replaced[i] = new Vector3Int(table[i].Day, table[i].Opens, table[i].Closes);
        Apply(schedule, replaced);

        var registration = Disposable.Create(() =>
        {
            // Only undo our own change: another mod may have set hours after us.
            if (schedule != null && ReferenceEquals(schedule.OpenTimes, replaced))
                Apply(schedule, original);
        });
        owner.Track(registration);
        return registration;
    }

    /// <summary>
    /// Where characters stand to use a door (the NPC waypoint in front of it), or null when the door has none.
    /// Handy to walk a mod NPC to a door. A scan.
    /// </summary>
    public static Vector3? StandPositionOf(Door door)
    {
        if (door == null)
            return null;
        foreach (var waypoint in ObjectIds.Active<NPC.Waypoint_Door>())
        {
            if (waypoint.door == door)
                return waypoint.transform.position;
        }
        return null;
    }

    // Forgetting the last check makes the schedule look at the clock again on its next tick.
    private static void Apply(DoorSchedule schedule, Vector3Int[] times)
    {
        schedule.OpenTimes = times;
        LastCheckField(schedule) = new Vector2Int(-1, -1);
    }

    private static IEnumerable<(int Day, int Opens, int Closes)> Table(Vector3Int[] times)
    {
        if (times == null)
            yield break;
        foreach (var entry in times)
            yield return (entry.x, entry.y, entry.z);
    }
}
