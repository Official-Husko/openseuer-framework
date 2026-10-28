using System;
using System.Collections.Generic;
using NPC;
using ObenFramework.Player;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// The game's NPCs: who exists, where they are and what they are doing, and when they appear in or
/// leave the scene you are in. An NPC is known from the schedule database the game simulates
/// (<c>NPCDirector.globalDatabase</c>, all of them, wherever they are) and from the objects in the
/// current scene. Reads build copies, so call them when something happened, not every frame.
/// </summary>
public static partial class Npcs
{
    /// <summary>Every NPC the game knows, in database order, followed by scene NPCs that have no database entry.</summary>
    public static List<NpcRecord> All()
    {
        var result = new List<NpcRecord>();
        var seen = new HashSet<int>();
        var database = NPCDirector.globalDatabase;
        if (database != null)
        {
            foreach (var data in database)
            {
                if (data == null || !seen.Add(data.ID))
                    continue;
                result.Add(new NpcRecord(data, LiveObject(data.ID)));
            }
        }

        foreach (var live in LiveObjects())
        {
            if (live != null && live.character != null && seen.Add(live.character.ID))
                result.Add(new NpcRecord(DataOf(live.character.ID), live));
        }
        return result;
    }

    /// <summary>The NPC with this character id, or null when the game has none.</summary>
    public static NpcRecord Get(int id)
    {
        var data = DataOf(id);
        var live = LiveObject(id);
        return data == null && live == null ? null : new NpcRecord(data, live);
    }

    /// <summary>
    /// Finds an NPC by first name or full name, ignoring case ("Pentti" or "Pentti Penttilä"). When more
    /// than one NPC shares a first name the first in the database wins; use the full name or the id then.
    /// </summary>
    public static NpcRecord Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        NpcRecord byFirstName = null;
        foreach (var npc in All())
        {
            if (string.Equals(npc.FullName, name, StringComparison.OrdinalIgnoreCase))
                return npc;
            if (byFirstName == null && string.Equals(npc.Name, name, StringComparison.OrdinalIgnoreCase))
                byFirstName = npc;
        }
        return byFirstName;
    }

    /// <summary>The NPCs that exist as objects in the scene you are in, switched off ones included.</summary>
    public static List<NpcRecord> InScene()
    {
        var result = new List<NpcRecord>();
        foreach (var live in LiveObjects())
        {
            if (live != null && live.character != null)
                result.Add(new NpcRecord(DataOf(live.character.ID), live));
        }
        return result;
    }

    /// <summary>
    /// The living, active NPC in this scene closest to <paramref name="from"/> (the player when omitted),
    /// within <paramref name="maxDistance"/> metres when that is above zero. Null when there is none, or no
    /// point to measure from.
    /// </summary>
    public static NpcRecord Nearest(Vector3? from = null, float maxDistance = 0f)
    {
        var origin = from ?? PlayerRef.Position;
        if (!origin.HasValue)
            return null;

        NPCInfo best = null;
        float bestSqr = maxDistance > 0f ? maxDistance * maxDistance : float.MaxValue;
        foreach (var live in LiveObjects())
        {
            if (live == null || live.character == null || !live.gameObject.activeInHierarchy || live.IsDead())
                continue;
            float sqr = (live.transform.position - origin.Value).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                best = live;
                bestSqr = sqr;
            }
        }
        return best == null ? null : new NpcRecord(DataOf(best.character.ID), best);
    }

    /// <summary>The NPC's object in the scene you are in, or null. Prefers an active one when there are two.</summary>
    public static NPCInfo LiveObject(int id)
    {
        var manager = NPCManager.instance;
        return manager != null ? manager.FindNpcById(id) : null;
    }

    /// <summary>The NPC's record in the schedule database, or null.</summary>
    public static NPCData DataOf(int id)
    {
        var director = NPCDirector.instance;
        return director != null ? director.GetNPCDataByID(id) : null;
    }

    private static readonly List<NPCInfo> NoObjects = new List<NPCInfo>();

    // The game's own list of NPC objects in the scene. Empty (never null) outside a level.
    private static List<NPCInfo> LiveObjects()
    {
        var manager = NPCManager.instance;
        return manager != null ? manager.GetAllNPCs() : NoObjects;
    }
}
