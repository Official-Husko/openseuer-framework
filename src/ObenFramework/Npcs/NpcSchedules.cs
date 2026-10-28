using System.Collections.Generic;
using NPC;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Npcs;

/// <summary>
/// NPC schedules. The game builds them from <c>StreamingAssets/NPCBehavior.json</c> into
/// <c>NPCDirector</c>'s database every start and never saves them, so what you read here is the same for
/// every save. Only a few NPCs have a timetable; most stand where they are placed. Reading works for NPCs in any scene.
/// Switching an entry off is the one change offered: the game skips disabled entries, and the entry is switched
/// back on when the mod unloads. Adding entries is not offered, because an entry needs the game's own
/// activity objects (nodes and groups of the scene) that cannot be built from outside.
/// </summary>
public static class NpcSchedules
{
    /// <summary>The NPC's timetable: entries tied to a day of the week and an hour. Empty when it has none.</summary>
    public static List<NpcScheduleEntry> Timetable(int npcId) => Read(npcId, random: false);

    /// <summary>The NPC's random activities: entries the game picks from when the timetable has nothing for now.</summary>
    public static List<NpcScheduleEntry> RandomActivities(int npcId) => Read(npcId, random: true);

    /// <summary>The schedule's name in the database, or null when the NPC has no schedule.</summary>
    public static string NameOf(int npcId) => Npcs.DataOf(npcId)?.scheduler?.Name;

    /// <summary>
    /// Switches a timetable (or random activity) entry off or on. <paramref name="index"/> is the entry's
    /// <see cref="NpcScheduleEntry.Index"/>. The previous value is put back when <paramref name="owner"/>
    /// unloads. Returns false when the NPC or entry does not exist.
    /// </summary>
    public static bool SetDisabled(ObenMod owner, int npcId, int index, bool disabled, bool random = false)
    {
        var action = ActionAt(npcId, index, random);
        if (action == null)
            return false;

        bool before = action.disabled;
        action.disabled = disabled;
        owner.Track(Disposable.Create(() =>
        {
            // Another mod may have changed it since; only undo our own change.
            if (action.disabled == disabled)
                action.disabled = before;
        }));
        return true;
    }

    private static List<NpcScheduleEntry> Read(int npcId, bool random)
    {
        var result = new List<NpcScheduleEntry>();
        var list = ListOf(npcId, random);
        if (list == null)
            return result;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                result.Add(new NpcScheduleEntry(list[i], i, random));
        }
        return result;
    }

    private static ScheduledAction ActionAt(int npcId, int index, bool random)
    {
        var list = ListOf(npcId, random);
        return list != null && index >= 0 && index < list.Count ? list[index] : null;
    }

    private static List<ScheduledAction> ListOf(int npcId, bool random)
    {
        var scheduler = Npcs.DataOf(npcId)?.scheduler;
        return scheduler == null ? null : random ? scheduler.RandomActivities : scheduler.Timetable;
    }
}
