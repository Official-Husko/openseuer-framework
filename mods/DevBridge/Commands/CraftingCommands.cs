using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Crafting;
using ObenFramework.Player;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Crafting stations, their control, speed and quality factors, and the crafting events.</summary>
internal static class CraftingCommands
{
    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        CraftingEvents.CraftStarted.Subscribe(owner, e => recorder.Add("CraftStarted", new JObject
        {
            ["recipe"] = e.RecipeId, ["name"] = RecipeName(e.RecipeId), ["station"] = e.Station.Id, ["kind"] = e.Station.Kind, ["multiples"] = e.Multiples,
        }));
        CraftingEvents.Crafted.Subscribe(owner, e => recorder.Add("Crafted", new JObject
        {
            ["recipe"] = e.RecipeId, ["name"] = RecipeName(e.RecipeId), ["station"] = e.Station.Id, ["success"] = e.Success,
            ["multiples"] = e.Multiples, ["results"] = ItemJson.Counts(e.Results),
        }));
        CraftingSources.Consumed.Subscribe(owner, e => recorder.Add("SourceConsumed", new JObject
        {
            ["from"] = e.SourceOwner, ["container"] = ObenFramework.World.Containers.IdOf(e.Storage), ["slot"] = e.Slot, ["itemId"] = e.ItemId,
            ["amount"] = e.Amount, ["liquidItemId"] = e.LiquidItemId, ["liquidAmount"] = Math.Round(e.LiquidAmount, 3),
        }));
        Recipes.Unlocked.Subscribe(owner, e => recorder.Add("RecipeUnlocked", new JObject { ["recipe"] = e.RecipeId, ["name"] = e.Name, ["learned"] = e.Learned }));
        Plants.Harvested.Subscribe(owner, e => recorder.Add("PlantHarvested", new JObject { ["plant"] = e.PlantId, ["items"] = ItemJson.Counts(e.Items) }));
        Plants.Picked.Subscribe(owner, e => recorder.Add("PlantPicked", new JObject { ["plant"] = e.PlantId, ["itemId"] = e.ItemId, ["amount"] = e.Amount }));
        Dishwashers.Finished.Subscribe(owner, e => recorder.Add("DishwasherFinished", new JObject { ["id"] = e.Id }));
        Recycling.Recycled.Subscribe(owner, e => recorder.Add("Recycled", new JObject { ["machine"] = e.MachineId, ["amount"] = e.Amount, ["money"] = e.Money }));
        Autofarms.Replanted.Subscribe(owner, e => recorder.Add("Replanted", new JObject { ["autofarm"] = e.AutofarmId, ["planted"] = e.Planted, ["initial"] = e.Initial }));
    });

    [BridgeCommand("crafting.stations", "Crafting stations of the loaded scenes, nearest first, with speed, quality and extra ingredient sources.",
        Params = new[] { "radius?: only within this many metres of the player", "kind?: only this class (manufacturing, manufacturingProcess, Growing, Dishwasher)",
            "max?: default 30" })]
    private static object Stations(JObject p)
    {
        float radius = p.Float("radius", 0f);
        string kind = p.Str("kind", null);
        int max = Math.Max(1, p.Int("max", 30));
        var origin = PlayerRef.Position;

        var entries = new List<(float distance, StationInfo info)>();
        foreach (var info in CraftingStations.All())
        {
            float distance = origin.HasValue ? Vector3.Distance(origin.Value, info.Position) : 0f;
            if ((radius > 0f && origin.HasValue && distance > radius) || (kind != null && info.Kind != kind))
                continue;
            entries.Add((distance, info));
        }
        entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        for (int i = 0; i < entries.Count && i < max; i++)
        {
            var info = entries[i].info;
            var row = ItemJson.Station(info);
            row["speed"] = Math.Round(CraftingSpeed.For(info.Station), 3);
            row["quality"] = Math.Round(CraftingQuality.For(info.Station), 3);
            row["selected"] = CraftingStations.Selected(info.Station) is Recipe recipe ? recipe.Name : null;
            row["extraSources"] = CraftingSources.For(info.Station).Count;
            if (origin.HasValue)
                row["distance"] = Math.Round(entries[i].distance, 2);
            list.Add(row);
        }
        return new JObject { ["total"] = entries.Count, ["open"] = CraftingStations.Open != null ? CraftingStations.IdOf(CraftingStations.Open) : null, ["stations"] = list };
    }

    [BridgeCommand("crafting.control", "Start, stop, pause or resume a station the way its buttons do.",
        Params = new[] { "id: station id from crafting.stations", "action: start, stop, pause or resume" })]
    private static object Control(JObject p)
    {
        string id = p.Str("id");
        if (CraftingStations.Find(id) == null)
            throw new ArgumentException($"No station '{id}'.");
        bool done;
        switch (p.Str("action"))
        {
            case "start": done = CraftingStations.Start(id); break;
            case "stop": done = CraftingStations.Stop(id); break;
            case "pause": done = CraftingStations.Pause(id); break;
            case "resume": done = CraftingStations.Resume(id); break;
            default: throw new ArgumentException("action must be start, stop, pause or resume.");
        }
        return new JObject { ["done"] = done, ["station"] = ItemJson.Station(CraftingStations.Describe(CraftingStations.Find(id))) };
    }

    [BridgeCommand("crafting.speed", "List the crafting speed factors, or set or clear the bridge's own for a key.",
        Params = new[] { "key?: * for all stations, a station class or a station name", "speed?: 0.01 to 100; omit to only list", "clear?: remove the bridge's factor for the key" })]
    private static object Speed(JObject p) => Factors(p, "speed", CraftingSpeed.Set, CraftingSpeed.Remove, CraftingSpeed.Entries);

    [BridgeCommand("crafting.quality", "List the crafting quality factors, or set or clear the bridge's own for a key.",
        Params = new[] { "key?: * for all stations, a station class or a station name", "factor?: above 0, at most 10; omit to only list", "clear?: remove the bridge's factor for the key" })]
    private static object Quality(JObject p) => Factors(p, "factor", CraftingQuality.Set, CraftingQuality.Remove, CraftingQuality.Entries);

    [BridgeCommand("crafting.events", "Crafting events (CraftStarted, Crafted, SourceConsumed, RecipeUnlocked, plant, dishwasher, recycling and autofarm events) since recording started.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object EventsList(JObject p) => Events.Read(p.Bool("clear", true));

    private static object Factors(JObject p, string valueName, Action<ObenLoader.Runtime.ITracker, string, float> set,
        Func<ObenLoader.Runtime.ITracker, string, bool> remove, Func<List<(object Owner, string Key, float Factor)>> entries)
    {
        string key = p.Str("key", null);
        if (key != null && p.Bool("clear", false))
            remove(DevBridgeMod.Instance, key);
        else if (key != null && p[valueName] != null)
            set(DevBridgeMod.Instance, key, p.Float(valueName));

        var list = new JArray();
        foreach (var entry in entries())
            list.Add(new JObject { ["owner"] = ItemJson.Owner(entry.Owner), ["key"] = entry.Key, [valueName] = Math.Round(entry.Factor, 4) });
        return new JObject { ["factors"] = list };
    }

    private static string RecipeName(int id) => Recipes.ById(id) is Recipe recipe ? recipe.Name : null;
}
