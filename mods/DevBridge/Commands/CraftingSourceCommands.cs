using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Crafting;
using ObenFramework.World;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>DevBridge's own extra ingredient sources and recipe filter, to try both out on real stations.</summary>
internal static class CraftingSourceCommands
{
    private sealed class Source
    {
        public IDisposable Registration;
        public string Container;
        public float Radius;
    }

    private static readonly Dictionary<string, Source> Sources = new Dictionary<string, Source>();
    private static IDisposable _filter;
    private static string _filterText;

    [BridgeCommand("crafting.sources", "DevBridge's extra ingredient sources: register one (a container, or every container near the station), change its priority or slot filter in place, or remove it. Lists every registration.",
        Params = new[] { "name?: which of DevBridge's sources, default bridge", "container?: a container id from containers.list", "radius?: or every container this near the station",
            "priority?: higher goes first; changes a registered source in place", "item?: slot filter, only slots holding this item id; -1 lifts it", "remove?: remove the named source",
            "clear?: remove all of DevBridge's sources" })]
    private static object Register(JObject p)
    {
        string name = p.Str("name", "bridge");
        if (p.Bool("clear", false))
        {
            foreach (var source in Sources.Values)
                source.Registration.Dispose();
            Sources.Clear();
        }
        else if (p.Bool("remove", false) && Sources.TryGetValue(name, out var gone))
        {
            gone.Registration.Dispose();
            Sources.Remove(name);
        }
        else
        {
            Update(name, p);
        }

        var list = new JArray();
        foreach (var entry in CraftingSources.Entries())
            list.Add(new JObject { ["owner"] = entry.Owner, ["priority"] = entry.Priority, ["filtered"] = entry.Filtered });
        var ours = new JObject();
        foreach (var pair in Sources)
            ours[pair.Key] = pair.Value.Container != null ? "container " + pair.Value.Container : $"within {pair.Value.Radius} m";
        return new JObject { ["registrations"] = list, ["bridge"] = ours };
    }

    [BridgeCommand("recipes.filter", "DevBridge's recipe filter: station windows only list recipes whose name contains the text. Without text it is removed.",
        Params = new[] { "text?: part of the recipe name" })]
    private static object Filter(JObject p)
    {
        _filterText = p.Str("text", null);
        if (string.IsNullOrEmpty(_filterText))
        {
            _filter?.Dispose();
            _filter = null;
        }
        else if (_filter == null)
        {
            _filter = RecipeFilter.Add(DevBridgeMod.Instance, c => RecipeFilter.NameContains(c.Recipe, _filterText));
        }
        RecipeFilter.Refresh();
        return new JObject { ["filter"] = _filter != null ? _filterText : null, ["active"] = RecipeFilter.Active };
    }

    private static void Update(string name, JObject p)
    {
        bool known = Sources.TryGetValue(name, out var source);
        if (!known && p["container"] == null && p["radius"] == null)
            return;
        if (!known)
        {
            source = new Source();
            var holder = source;
            source.Registration = CraftingSources.Register(DevBridgeMod.Instance, station => Containers(holder, station), p.Int("priority", 0));
            Sources[name] = source;
        }
        else if (p["priority"] != null)
        {
            CraftingSources.SetPriority(source.Registration, p.Int("priority", 0));
        }

        if (p["container"] != null)
        {
            string id = p.Str("container");
            if (ObenFramework.World.Containers.Find(id) == null)
                throw new ArgumentException($"No container '{id}'.");
            source.Container = id;
        }
        else if (p["radius"] != null)
        {
            source.Container = null;
            source.Radius = p.Float("radius");
        }

        if (p["item"] != null)
        {
            int item = p.Int("item", -1);
            CraftingSources.SetSlotFilter(source.Registration, item < 0 ? null : (Func<Storage, int, StorageSlot, bool>)((_, _, slot) => SlotHolds(slot, item)));
        }
    }

    private static IEnumerable<Storage> Containers(Source source, StationInfo station)
    {
        if (source.Container != null)
        {
            var storage = ObenFramework.World.Containers.Find(source.Container);
            if (storage != null)
                yield return storage;
            yield break;
        }
        foreach (var info in ObenFramework.World.Containers.List(false))
        {
            if (Vector3.Distance(info.Position, station.Position) <= source.Radius)
                yield return ObenFramework.World.Containers.Find(info.Id);
        }
    }

    private static bool SlotHolds(StorageSlot slot, int item) => slot != null && slot.itemReference != null && slot.itemReference.Item != null && slot.itemReference.Item.ID == item;
}
