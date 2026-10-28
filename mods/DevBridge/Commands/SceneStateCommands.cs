using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.World.Logic;

namespace DevBridge.Commands;

/// <summary>The active scene's logic objects (branches, counters, states, sequences, timers, relays) through ObenFramework.World.Logic.</summary>
internal static class SceneStateCommands
{
    [BridgeCommand("scene.state", "The active scene's logic objects and their state, with ids for scene.set and scene.fire.",
        Params = new[] { "kind?: branches, counters, states, sequences, timers or relays; default all", "match?: only ids containing this text" })]
    private static object State(JObject p)
    {
        string kind = p.Str("kind", "all").ToLowerInvariant();
        string match = p.Str("match", null);
        var result = new JObject();
        if (Wants(kind, "branches"))
            result["branches"] = Filter(match, SceneState.Branches(), b => b.Id, b => new JObject { ["id"] = b.Id, ["value"] = b.Value });
        if (Wants(kind, "counters"))
            result["counters"] = Filter(match, SceneState.Counters(), c => c.Id, c => new JObject { ["id"] = c.Id, ["value"] = c.Value, ["max"] = c.Max });
        if (Wants(kind, "states"))
            result["states"] = Filter(match, SceneState.States(), s => s.Id, s => new JObject { ["id"] = s.Id, ["current"] = s.Current, ["names"] = new JArray(s.Names) });
        if (Wants(kind, "sequences"))
            result["sequences"] = Filter(match, SceneState.Sequences(), s => s.Id, s => new JObject { ["id"] = s.Id, ["entries"] = s.Entries, ["loop"] = s.Loop });
        if (Wants(kind, "timers"))
            result["timers"] = Filter(match, SceneState.Timers(), t => t.Id, t => new JObject { ["id"] = t.Id, ["interval"] = t.Interval, ["running"] = t.Running, ["disabled"] = t.Disabled });
        if (Wants(kind, "relays"))
            result["relays"] = Filter(match, SceneLogic.Relays(), r => r.Id, r => new JObject { ["id"] = r.Id, ["fireOnce"] = r.FireOnce, ["fired"] = r.Fired, ["enabled"] = r.Enabled });
        return result;
    }

    [BridgeCommand("scene.set", "Change one logic object of the active scene through the game's own methods.",
        Params = new[]
        {
            "kind: branch, counter, add, state, sequence, timer, interval or outputs", "id: from scene.state",
            "value: branch true/false, counter value, add amount, state name, sequence/timer start/stop, interval seconds, outputs true/false",
            "trigger?: for state, also fire the new state's relay",
        })]
    private static object Set(JObject p)
    {
        string id = p.Str("id");
        string kind = p.Str("kind").ToLowerInvariant();
        string value = p["value"]?.ToString() ?? throw new ArgumentException("Missing parameter 'value'.");
        bool done;
        switch (kind)
        {
            case "branch": done = SceneState.SetBranch(id, bool.Parse(value)); break;
            case "counter": done = SceneState.SetCounter(id, int.Parse(value)); break;
            case "add": done = SceneState.AddCounter(id, int.Parse(value)); break;
            case "state": done = SceneState.SetState(id, value, p.Bool("trigger", false)); break;
            case "sequence": done = value == "stop" ? SceneState.StopSequence(id) : SceneState.StartSequence(id); break;
            case "timer": done = value == "stop" ? SceneState.StopTimer(id) : SceneState.StartTimer(id); break;
            case "interval": done = SceneState.SetInterval(id, float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)); break;
            case "outputs": done = SceneLogic.SetEnabled(id, bool.Parse(value)); break;
            default: throw new ArgumentException($"Unknown kind '{kind}'.");
        }
        return new JObject { ["done"] = done };
    }

    [BridgeCommand("scene.fire", "Fire a relay of the active scene as the level would (only if it would fire now).",
        Params = new[] { "id: relay id from scene.state" })]
    private static object Fire(JObject p) => new JObject { ["fired"] = SceneLogic.Fire(p.Str("id")) };

    private static bool Wants(string kind, string name) => kind == "all" || kind == name;

    private static JArray Filter<T>(string match, System.Collections.Generic.List<T> items, Func<T, string> id, Func<T, JObject> describe)
    {
        var list = new JArray();
        foreach (var item in items)
        {
            if (match == null || id(item).IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0)
                list.Add(describe(item));
        }
        return list;
    }
}
