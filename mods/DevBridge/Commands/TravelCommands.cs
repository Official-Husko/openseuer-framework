using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using ObenFramework.Transport;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Level exits, trips between levels, entrypoints, elevators, ladders and tarp doors.</summary>
internal static class TravelCommands
{
    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        Levels.Leaving.Subscribe(owner, t => recorder.Add("Leaving", Trip(t)));
        Levels.Arrived.Subscribe(owner, t => recorder.Add("Arrived", Trip(t)));
        Elevators.Called.Subscribe(owner, e => recorder.Add("ElevatorCalled", new JObject { ["id"] = e.Id, ["floor"] = e.Floor, ["source"] = e.Source.ToString() }));
        Elevators.Arrived.Subscribe(owner, e => recorder.Add("ElevatorArrived", new JObject { ["id"] = e.Id, ["floor"] = e.Floor }));
        Ladders.Climbed.Subscribe(owner, e => recorder.Add("Ladder", new JObject { ["id"] = e.Id, ["climbing"] = e.Climbing }));
        TarpDoors.Changed.Subscribe(owner, e => recorder.Add("TarpDoor", new JObject { ["id"] = e.Id, ["open"] = e.Open, ["source"] = e.Source.ToString() }));
    });

    [BridgeCommand("levels.list", "Exits of the current level (doors and triggers to other scenes), nearest first.",
        Params = new[] { "scene?: only exits to this scene", "max?: default 40" })]
    private static object LevelsList(JObject p)
    {
        string scene = p.Str("scene", null);
        var origin = PlayerRef.Position;
        var entries = new List<(float distance, LevelExit exit)>();
        foreach (var exit in Levels.List())
        {
            if (scene != null && !string.Equals(exit.Scene, scene, StringComparison.OrdinalIgnoreCase))
                continue;
            entries.Add((origin.HasValue ? Vector3.Distance(origin.Value, exit.Position) : 0f, exit));
        }
        entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        int max = Math.Max(1, p.Int("max", 40));
        for (int i = 0; i < entries.Count && i < max; i++)
        {
            var exit = entries[i].exit;
            list.Add(new JObject
            {
                ["id"] = exit.Id, ["title"] = exit.Title, ["scene"] = exit.Scene, ["entrypoint"] = exit.Entrypoint, ["name"] = exit.Name,
                ["door"] = exit.IsDoor, ["locked"] = exit.Locked, ["distance"] = Math.Round(entries[i].distance, 1), ["pos"] = ItemJson.Vec(exit.Position),
                ["stand"] = exit.Stand.HasValue ? ItemJson.Vec(exit.Stand.Value) : null,
            });
        }
        bool can = Levels.CanTravel(out string reason);
        return new JObject { ["total"] = entries.Count, ["canTravel"] = can, ["reason"] = reason, ["exits"] = list };
    }

    [BridgeCommand("levels.use", "Uses an exit like the player would (doors animate first). Changes the level.",
        Params = new[] { "id: exit id from levels.list", "ignoreLock?: open a locked door anyway, default false" })]
    private static object LevelsUse(JObject p)
    {
        var exit = Levels.Find(p.Str("id")) ?? throw new ArgumentException($"No exit '{p.Str("id")}'.");
        bool started = Levels.Use(exit, p.Bool("ignoreLock", false));
        Levels.CanTravel(out string reason);
        return new JObject { ["started"] = started, ["scene"] = exit.OtherLevel, ["reason"] = started ? null : reason ?? "locked or missing scene" };
    }

    [BridgeCommand("levels.lock", "Locks or unlocks a door to another level.", Params = new[] { "id: exit id", "locked: true or false" })]
    private static object LevelsLock(JObject p)
    {
        var exit = Levels.Find(p.Str("id")) ?? throw new ArgumentException($"No exit '{p.Str("id")}'.");
        return new JObject { ["ok"] = Levels.SetLocked(exit, p.Bool("locked")), ["locked"] = Levels.Describe(exit).Locked };
    }

    [BridgeCommand("travel.go", "Sends the player to a scene (saves first, like any level change).",
        Params = new[] { "scene: scene name (see travel.scenes)", "entrypoint?: where to arrive, default the level's spawn" })]
    private static object TravelGo(JObject p)
    {
        string scene = p.Str("scene");
        if (!Levels.Exists(scene))
            throw new ArgumentException($"No scene '{scene}'.");
        bool started = Levels.ChangeTo(scene, p.Str("entrypoint", null));
        Levels.CanTravel(out string reason);
        return new JObject { ["started"] = started, ["reason"] = reason };
    }

    [BridgeCommand("travel.scenes", "Every scene of the build, in build order.")]
    private static object TravelScenes(JObject p) => new JObject { ["scenes"] = new JArray(Levels.Scenes) };

    [BridgeCommand("travel.entrypoints", "Entrypoints of the current level (where players arrive).")]
    private static object TravelEntrypoints(JObject p) =>
        new JObject { ["entrypoints"] = new JArray(Travel.Entrypoints()), ["usingMinitrain"] = Travel.UsingMinitrain };

    [BridgeCommand("travel.entrypoint", "Adds an entrypoint owned by the bridge to a scene, at the player or a point, or removes one the bridge added.",
        Params = new[] { "name: entrypoint name", "scene?: default the current level", "x?, y?, z?: default the player's position", "yaw?: facing, default the player's",
            "remove?: true takes the bridge's entrypoint back" })]
    private static object TravelEntrypoint(JObject p)
    {
        string name = p.Str("name");
        string scene = p.Str("scene", ObenFramework.Game.WorldState.SceneName);
        if (p.Bool("remove", false))
            return new JObject { ["removed"] = Travel.RemoveEntrypoint(DevBridgeMod.Instance, scene, name) };
        var at = p["x"] != null ? new Vector3(p.Float("x"), p.Float("y"), p.Float("z")) : PlayerRef.Position ?? throw new ArgumentException("Give x, y and z.");
        float yaw = p["yaw"] != null ? p.Float("yaw") : PlayerRef.Yaw ?? 0f;
        Travel.AddEntrypoint(DevBridgeMod.Instance, scene, name, at, yaw);
        return new JObject { ["scene"] = scene, ["name"] = name, ["pos"] = ItemJson.Vec(at), ["yaw"] = Math.Round(yaw, 1), ["here"] = Travel.Entrypoints().Contains(name) };
    }

    [BridgeCommand("travel.teleport", "Moves the player to an entrypoint of the current level.", Params = new[] { "name: entrypoint name" })]
    private static object TravelTeleport(JObject p) => new JObject { ["ok"] = Travel.TeleportTo(p.Str("name")) };

    [BridgeCommand("travel.events", "Trips, elevator, ladder and tarp door events since recording started.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object TravelEvents(JObject p) => Events.Read(p.Bool("clear", true));

    [BridgeCommand("elevators.list", "Elevators of the loaded scenes with floors, queue and power.")]
    private static object ElevatorsList(JObject p)
    {
        var list = new JArray();
        foreach (var elevator in Elevators.All())
        {
            var info = Elevators.Describe(elevator);
            list.Add(new JObject
            {
                ["id"] = info.Id, ["name"] = elevator.name, ["floors"] = info.Floors, ["position"] = Math.Round(info.Position, 2), ["floor"] = info.Floor,
                ["moving"] = info.Moving, ["powered"] = info.Powered, ["queue"] = new JArray(info.Queue), ["enabled"] = new JArray(info.FloorsEnabled),
            });
        }
        return new JObject { ["elevators"] = list };
    }

    [BridgeCommand("elevators.call", "Calls an elevator to a floor.", Params = new[] { "id: elevator id", "floor: floor index from 0" })]
    private static object ElevatorsCall(JObject p)
    {
        var elevator = Elevators.Find(p.Str("id")) ?? throw new ArgumentException($"No elevator '{p.Str("id")}'.");
        return new JObject { ["ok"] = Elevators.Call(elevator, p.Int("floor", 0)) };
    }

    [BridgeCommand("ladders.list", "Ladders of the loaded scenes.")]
    private static object LaddersList(JObject p)
    {
        var list = new JArray();
        foreach (var ladder in Ladders.All())
            list.Add(new JObject { ["id"] = Ladders.IdOf(ladder), ["blocked"] = ladder.blocked, ["playerOn"] = Ladders.IsPlayerOn(ladder), ["pos"] = ItemJson.Vec(ladder.transform.position) });
        return new JObject { ["playerClimbing"] = Ladders.PlayerClimbing, ["ladders"] = list };
    }

    [BridgeCommand("ladders.block", "Blocks or frees a ladder.", Params = new[] { "id: ladder id", "blocked: true or false" })]
    private static object LaddersBlock(JObject p)
    {
        var ladder = Ladders.Find(p.Str("id")) ?? throw new ArgumentException($"No ladder '{p.Str("id")}'.");
        return new JObject { ["ok"] = Ladders.SetBlocked(ladder, p.Bool("blocked")) };
    }

    [BridgeCommand("tarps.list", "Tarp doors (cloth curtains) of the loaded scenes.")]
    private static object TarpsList(JObject p)
    {
        var list = new JArray();
        foreach (var tarp in TarpDoors.All())
            list.Add(new JObject { ["id"] = TarpDoors.IdOf(tarp), ["open"] = TarpDoors.IsOpen(tarp), ["pos"] = ItemJson.Vec(tarp.transform.position) });
        return new JObject { ["tarps"] = list };
    }

    [BridgeCommand("tarps.set", "Opens or closes a tarp door.", Params = new[] { "id: tarp id", "open: true or false" })]
    private static object TarpsSet(JObject p)
    {
        var tarp = TarpDoors.Find(p.Str("id")) ?? throw new ArgumentException($"No tarp door '{p.Str("id")}'.");
        bool ok = TarpDoors.SetOpen(tarp, p.Bool("open"));
        return new JObject { ["ok"] = ok, ["open"] = TarpDoors.IsOpen(tarp) };
    }

    private static JObject Trip(LevelTrip t) => new JObject
    {
        ["from"] = t.From, ["to"] = t.To, ["entrypoint"] = t.Entrypoint, ["exit"] = t.ExitId, ["source"] = t.Source.ToString(),
    };
}
