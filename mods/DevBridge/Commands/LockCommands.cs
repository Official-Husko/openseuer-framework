using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using ObenFramework.World;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Key locks, keypads and door opening hours, through ObenFramework.World.</summary>
internal static class LockCommands
{
    private static readonly Dictionary<string, IDisposable> Hours = new Dictionary<string, IDisposable>();

    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        Locks.Changed.Subscribe(owner, e => recorder.Add(e.Locked ? "Locked" : "Unlocked", new JObject
        {
            ["id"] = e.Id, ["name"] = e.Owner != null ? e.Owner.name : null, ["source"] = e.Source.ToString(),
        }));
        Locks.Lockpicked.Subscribe(owner, e => recorder.Add("Lockpicked", new JObject { ["id"] = e.Id, ["success"] = e.Success }));
        Keypads.Changed.Subscribe(owner, e => recorder.Add("Keypad", new JObject { ["id"] = e.Id, ["kind"] = e.Kind.ToString(), ["source"] = e.Source.ToString() }));
    });

    [BridgeCommand("locks.list", "Key locks of the loaded scenes (doors, level doors, storages, stations, switches), nearest first.",
        Params = new[] { "radius?: only within this many metres of the player", "lockedOnly?: default false", "max?: default 40" })]
    private static object LocksList(JObject p)
    {
        float radius = p.Float("radius", 0f);
        bool lockedOnly = p.Bool("lockedOnly", false);
        var origin = PlayerRef.Position;
        var entries = new List<(float distance, Component owner)>();
        foreach (var owner in Locks.All())
        {
            float distance = origin.HasValue ? Vector3.Distance(origin.Value, owner.transform.position) : 0f;
            if ((radius > 0f && origin.HasValue && distance > radius) || (lockedOnly && !Locks.IsLocked(owner)))
                continue;
            entries.Add((distance, owner));
        }
        entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        int max = Math.Max(1, p.Int("max", 40));
        for (int i = 0; i < entries.Count && i < max; i++)
        {
            var info = Locks.Describe(entries[i].owner);
            list.Add(new JObject
            {
                ["id"] = info.Id, ["name"] = info.Owner.name, ["kind"] = info.Kind.ToString(), ["locked"] = info.Locked,
                ["lockpickable"] = info.Lockpickable, ["difficulty"] = Math.Round(info.Difficulty, 2), ["key"] = info.KeyName,
                ["playerHasKey"] = info.PlayerHasKey, ["relockMinutes"] = info.RelockMinutes, ["distance"] = Math.Round(entries[i].distance, 1),
            });
        }
        return new JObject { ["total"] = entries.Count, ["locks"] = list };
    }

    [BridgeCommand("locks.set", "Changes a lock: locked, lockpickable and difficulty (each optional).",
        Params = new[] { "id: lock id from locks.list", "locked?: true or false", "lockpickable?: true or false", "difficulty?: 0 to 2" })]
    private static object LocksSet(JObject p)
    {
        var owner = Locks.Find(p.Str("id")) ?? throw new ArgumentException($"No lock '{p.Str("id")}'.");
        bool ok = true;
        if (p["lockpickable"] != null)
            ok &= Locks.SetLockpickable(owner, p.Bool("lockpickable"));
        if (p["difficulty"] != null)
            ok &= Locks.SetDifficulty(owner, p.Float("difficulty"));
        if (p["locked"] != null)
            ok &= Locks.SetLocked(owner, p.Bool("locked"));
        var info = Locks.Describe(owner);
        return new JObject { ["ok"] = ok, ["locked"] = info.Locked, ["lockpickable"] = info.Lockpickable, ["difficulty"] = Math.Round(info.Difficulty, 2) };
    }

    [BridgeCommand("locks.givekey", "Gives the player the key of a lock.", Params = new[] { "id: lock id" })]
    private static object LocksGiveKey(JObject p)
    {
        var owner = Locks.Find(p.Str("id")) ?? throw new ArgumentException($"No lock '{p.Str("id")}'.");
        var key = Locks.KeyOf(owner);
        return new JObject { ["key"] = key != null ? key.itemName : null, ["given"] = Locks.GiveKey(owner) };
    }

    [BridgeCommand("locks.events", "Lock, lockpick and keypad events since recording started.", Params = new[] { "clear?: default true" })]
    private static object LocksEvents(JObject p) => Events.Read(p.Bool("clear", true));

    [BridgeCommand("keypads.list", "Keypads and rotary locks of the loaded scenes with their codes.")]
    private static object KeypadsList(JObject p)
    {
        var list = new JArray();
        foreach (var keypad in Keypads.All())
        {
            list.Add(new JObject
            {
                ["id"] = Keypads.IdOf(keypad), ["name"] = keypad.name, ["open"] = Keypads.IsOpen(keypad), ["power"] = Keypads.HasPower(keypad),
                ["rotary"] = Keypads.IsRotaryLock(keypad), ["code"] = Code(keypad),
            });
        }
        return new JObject { ["keypads"] = list };
    }

    [BridgeCommand("keypads.set", "Opens or locks a keypad, sets its code or plays its wrong-code reaction.",
        Params = new[] { "id: keypad id", "open?: true or false", "code?: digits (4711), or numbers with commas for a rotary lock (17,3,22,5)", "refuse?: true plays the wrong-code reaction" })]
    private static object KeypadsSet(JObject p)
    {
        var keypad = Keypads.Find(p.Str("id")) ?? throw new ArgumentException($"No keypad '{p.Str("id")}'.");
        bool ok = true;
        string code = p.Str("code", null);
        if (code != null)
        {
            var parts = code.Contains(",") ? code.Split(',') : Array.ConvertAll(code.ToCharArray(), c => c.ToString());
            ok &= Keypads.SetCode(keypad, Array.ConvertAll(parts, part => int.Parse(part.Trim())));
        }
        if (p["open"] != null)
            ok &= Keypads.SetOpen(keypad, p.Bool("open"));
        if (p.Bool("refuse", false))
            ok &= Keypads.Refuse(keypad);
        return new JObject { ["ok"] = ok, ["open"] = Keypads.IsOpen(keypad), ["code"] = Code(keypad) };
    }

    private static string Code(Keypad keypad) => string.Join(Keypads.IsRotaryLock(keypad) ? "," : "", Keypads.CodeOf(keypad));

    [BridgeCommand("doors.schedule", "Opening hours of door schedules (shops). With 'opens' and 'closes' sets the same hours every day; 'reset' restores the level's.",
        Params = new[] { "id?: schedule id, or a door id to find its schedule", "opens?: hour 0-23", "closes?: hour 1-24", "reset?: true restores the level's hours" })]
    private static object DoorsSchedule(JObject p)
    {
        string id = p.Str("id", null);
        var targets = new List<DoorSchedule>();
        if (id == null)
            targets.AddRange(DoorSchedules.All());
        else
        {
            var schedule = DoorSchedules.Find(id);
            if (schedule == null)
                schedule = DoorSchedules.Of(Doors.Find(id));
            if (schedule == null)
                throw new ArgumentException($"No schedule or scheduled door '{id}'.");
            targets.Add(schedule);
        }

        foreach (var schedule in targets)
        {
            string key = DoorSchedules.IdOf(schedule);
            if (p.Bool("reset", false) && Hours.TryGetValue(key, out var old))
            {
                old.Dispose();
                Hours.Remove(key);
            }
            else if (id != null && p["opens"] != null && p["closes"] != null)
            {
                if (Hours.TryGetValue(key, out var previous))
                    previous.Dispose();
                Hours[key] = DoorSchedules.SetHours(DevBridgeMod.Instance, schedule, DoorScheduleRules.EveryDay(p.Int("opens", 8), p.Int("closes", 20)));
            }
        }

        var list = new JArray();
        foreach (var schedule in targets)
        {
            var doors = new JArray();
            foreach (var door in schedule.Doors ?? Array.Empty<Door>())
            {
                if (door != null)
                    doors.Add(Doors.IdOf(door));
            }
            var hours = new JArray();
            foreach (var entry in DoorSchedules.HoursOf(schedule))
                hours.Add(entry.ToString());
            list.Add(new JObject { ["id"] = DoorSchedules.IdOf(schedule), ["name"] = schedule.name, ["openNow"] = DoorSchedules.IsOpenNow(schedule), ["doors"] = doors, ["hours"] = hours });
        }
        return new JObject { ["schedules"] = list };
    }
}
