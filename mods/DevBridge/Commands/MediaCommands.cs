using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Assets;
using ObenFramework.World;

namespace DevBridge.Commands;

/// <summary>Radios, televisions, the guitar, item icons and models, and translated texts.</summary>
internal static class MediaCommands
{
    private static readonly Dictionary<int, IDisposable> Models = new Dictionary<int, IDisposable>();

    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        Radios.Changed.Subscribe(owner, r => recorder.Add("Radio", new JObject { ["id"] = r.Id, ["enabled"] = r.Enabled, ["station"] = r.Station }));
        Televisions.Changed.Subscribe(owner, t => recorder.Add("Television", new JObject { ["id"] = t.Id, ["on"] = t.On, ["powered"] = t.Powered }));
        Instruments.Strummed.Subscribe(owner, g => recorder.Add("Strum", new JObject { ["guitar"] = g != null ? g.name : null }));
        Instruments.Commented.Subscribe(owner, c => recorder.Add("GuitarComment", new JObject { ["npc"] = c.NpcId, ["positive"] = c.Positive }));
    });

    [BridgeCommand("radios.list", "Radios of the loaded scenes with power and station.")]
    private static object RadiosList(JObject p)
    {
        var list = new JArray();
        foreach (var radio in Radios.All())
        {
            var info = Radios.Describe(radio);
            list.Add(new JObject { ["id"] = info.Id, ["title"] = radio.title, ["enabled"] = info.Enabled, ["station"] = info.Station, ["stations"] = info.Stations, ["pos"] = ItemJson.Vec(radio.transform.position) });
        }
        return new JObject { ["radios"] = list };
    }

    [BridgeCommand("radios.set", "Tunes a radio (0 is off) and/or gives or cuts its power.", Params = new[] { "id: radio id", "station?: 0 and up", "enabled?: true or false" })]
    private static object RadiosSet(JObject p)
    {
        var radio = Radios.Find(p.Str("id"));
        if (radio == null)
            throw new ArgumentException($"No radio '{p.Str("id")}'.");
        if (p["enabled"] != null)
            Radios.SetEnabled(radio, p.Bool("enabled"));
        if (p["station"] != null)
            Radios.SetStation(radio, p.Int("station", 0));
        var info = Radios.Describe(radio);
        return new JObject { ["enabled"] = info.Enabled, ["station"] = info.Station };
    }

    [BridgeCommand("tvs.list", "Televisions of the loaded scenes.")]
    private static object TvsList(JObject p)
    {
        var list = new JArray();
        foreach (var tv in Televisions.All())
        {
            var info = Televisions.Describe(tv);
            list.Add(new JObject { ["id"] = info.Id, ["on"] = info.On, ["powered"] = info.Powered, ["locked"] = info.Locked, ["pos"] = ItemJson.Vec(tv.transform.position) });
        }
        return new JObject { ["tvs"] = list };
    }

    [BridgeCommand("tvs.set", "Switches a television on or off.", Params = new[] { "id: tv id", "on: true or false" })]
    private static object TvsSet(JObject p)
    {
        var tv = Televisions.Find(p.Str("id"));
        if (tv == null)
            throw new ArgumentException($"No television '{p.Str("id")}'.");
        return new JObject { ["ok"] = Televisions.SetOn(tv, p.Bool("on")) };
    }

    [BridgeCommand("guitar.play", "The guitar in the player's hands: state, one strum, a song, or a held note.",
        Params = new[] { "strum?: true strums once", "song?: waits in seconds, e.g. [0,0.5,0.5,1]", "hold?: long note index", "stop?: stops song and note" })]
    private static object Guitar(JObject p)
    {
        var guitar = Instruments.Held;
        if (guitar == null)
            return new JObject { ["held"] = false };
        bool? result = null;
        if (p.Bool("stop", false))
        {
            Instruments.StopSong(guitar);
            Instruments.StopHold();
        }
        else if (p.Bool("strum", false))
            result = Instruments.Strum(guitar);
        else if (p["song"] is JArray song)
        {
            var waits = new List<float>();
            foreach (var wait in song)
                waits.Add(wait.Value<float>());
            Instruments.PlaySong(DevBridgeMod.Instance, guitar, waits);
            result = true;
        }
        else if (p["hold"] != null)
            result = Instruments.Hold(guitar, p.Int("hold", 0));
        return new JObject { ["held"] = true, ["canPlay"] = Instruments.CanPlay(guitar), ["holdNotes"] = Instruments.HoldNotes(guitar), ["result"] = result };
    }

    [BridgeCommand("media.events", "Radio, television and guitar events since recording started.", Params = new[] { "clear?: default true" })]
    private static object MediaEvents(JObject p) => Events.Read(p.Bool("clear", true));

    [BridgeCommand("text.get", "A text in the player's language (mod texts, then the game's string table, then English, then the key), and the languages.",
        Params = new[] { "key?: text key", "table?: string table, default Menu" })]
    private static object Text(JObject p)
    {
        string key = p.Str("key", null);
        return new JObject
        {
            ["language"] = GameText.Language, ["languages"] = new JArray(GameText.Languages()),
            ["text"] = key != null ? GameText.Get(key, p.Str("table", GameText.DefaultTable)) : null,
        };
    }

    [BridgeCommand("items.model", "Shows a loaded model for an item lying in the world or in hand (owned by the bridge), or puts the item's own look back with clear.",
        Params = new[] { "item: item id", "model?: full model name (see assets.list)", "held?: in hand instead of in the world", "clear?: true" })]
    private static object ItemModel(JObject p)
    {
        int item = p.Int("item", 0);
        if (Models.TryGetValue(item, out var old))
        {
            old.Dispose();
            Models.Remove(item);
        }
        if (p.Bool("clear", false))
            return new JObject { ["cleared"] = true };
        var model = ModelRegistry.Find(p.Str("model")) ?? throw new ArgumentException($"No model '{p.Str("model")}' is loaded.");
        Models[item] = p.Bool("held", false)
            ? ItemModels.SetHeldModel(DevBridgeMod.Instance, item, model)
            : ItemModels.SetWorldModel(DevBridgeMod.Instance, item, model);
        return new JObject { ["ok"] = true };
    }
}
