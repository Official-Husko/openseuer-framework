using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Npcs;
using ObenFramework.Player;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>NPCs and phone numbers, through ObenFramework.Npcs. Read only.</summary>
internal static class NpcCommands
{
    [BridgeCommand("npcs.list", "NPCs the game knows (scene and off-scene) with scene, position, activity and whether they are loaded.",
        Params = new[] { "loaded?: only NPCs that exist in the current scene", "scene?: only NPCs placed in this scene", "name?: only names containing this text",
            "max?: default 100" })]
    private static object List(JObject p)
    {
        bool loadedOnly = p.Bool("loaded", false);
        string scene = p.Str("scene", null);
        string name = p.Str("name", null);
        int max = Math.Max(1, p.Int("max", 100));

        var list = new JArray();
        int matched = 0;
        foreach (var npc in Npcs.All())
        {
            if (loadedOnly && !npc.Loaded)
                continue;
            if (scene != null && !string.Equals(npc.Scene, scene, StringComparison.OrdinalIgnoreCase))
                continue;
            if (name != null && npc.FullName.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            matched++;
            if (list.Count < max)
                list.Add(Describe(npc));
        }
        return new JObject { ["total"] = matched, ["npcs"] = list };
    }

    [BridgeCommand("npcs.get", "One NPC by id or name, with its schedule (timetable and random activities).",
        Params = new[] { "id?: character id", "name?: first or full name", "schedule?: include the schedule, default true" })]
    private static object Get(JObject p)
    {
        var npc = p["id"] != null ? Npcs.Get(p.Int("id", -1)) : Npcs.Find(p.Str("name"));
        if (npc == null)
            throw new ArgumentException("No such NPC.");

        var result = Describe(npc);
        result["job"] = npc.Job;
        result["state"] = npc.State;
        result["claimedBy"] = NpcControl.ClaimedBy(npc.Id);
        if (p.Bool("schedule", true))
        {
            result["scheduleName"] = NpcSchedules.NameOf(npc.Id);
            result["timetable"] = Entries(NpcSchedules.Timetable(npc.Id));
            result["random"] = Entries(NpcSchedules.RandomActivities(npc.Id));
        }
        return result;
    }

    [BridgeCommand("npcs.nearest", "The living NPC in the scene closest to the player (or to a point).",
        Params = new[] { "x?,y?,z? or pos?: [x,y,z] to measure from, default the player", "maxDistance?: metres, default no limit" })]
    private static object Nearest(JObject p)
    {
        var npc = Npcs.Nearest(p.Position(), p.Float("maxDistance", 0f));
        if (npc == null)
            return new JObject { ["npc"] = null };

        var origin = p.Position() ?? PlayerRef.Position;
        var result = Describe(npc);
        result["distance"] = origin.HasValue ? Round(Vector3.Distance(origin.Value, npc.Position)) : (float?)null;
        return result;
    }

    [BridgeCommand("phone.list", "Every number in the game's phone book, with owner (null for the game's own), network and conversation.",
        Params = new[] { "mods?: only numbers added by mods, default false", "check?: instead, whether this number could be registered and why not" })]
    private static object PhoneList(JObject p)
    {
        string check = p.Str("check", null);
        if (check != null)
            return new JObject { ["number"] = check, ["free"] = PhoneNumbers.CanRegister(check, out string reason), ["reason"] = reason };
        bool modsOnly = p.Bool("mods", false);
        var list = new JArray();
        foreach (var number in PhoneNumbers.List())
        {
            if (modsOnly && number.Owner == null)
                continue;
            list.Add(new JObject
            {
                ["number"] = number.Number,
                ["name"] = number.Name,
                ["network"] = number.Network.ToString(),
                ["answerChance"] = Round(number.AnswerChance),
                ["conversation"] = number.Conversation,
                ["dialable"] = number.Dialable,
                ["owner"] = number.Owner,
            });
        }
        return new JObject { ["total"] = list.Count, ["numbers"] = list };
    }

    private static JObject Describe(NpcRecord npc) => new JObject
    {
        ["id"] = npc.Id,
        ["name"] = npc.FullName,
        ["scene"] = npc.Scene,
        ["pos"] = Vec(npc.Position),
        ["loaded"] = npc.Loaded,
        ["active"] = npc.Active,
        ["activity"] = npc.Activity,
        ["entry"] = npc.ScheduleEntry,
        ["hasSchedule"] = npc.HasSchedule,
        ["dead"] = npc.Dead,
    };

    private static JArray Entries(List<NpcScheduleEntry> entries)
    {
        var list = new JArray();
        foreach (var e in entries)
        {
            list.Add(new JObject
            {
                ["index"] = e.Index,
                ["name"] = e.Name,
                ["day"] = e.Random ? (int?)null : e.WeekDay,
                ["startHour"] = e.Random ? (float?)null : Round(e.StartHour),
                ["durationMinutes"] = Round(e.DurationMinutes),
                ["chance"] = Round(e.Chance),
                ["activity"] = e.Activity,
                ["place"] = e.Place,
                ["disabled"] = e.Disabled,
            });
        }
        return list;
    }

    private static float Round(float v) => Mathf.Round(v * 100f) / 100f;

    private static JArray Vec(Vector3 v) => new JArray(Round(v.x), Round(v.y), Round(v.z));
}
