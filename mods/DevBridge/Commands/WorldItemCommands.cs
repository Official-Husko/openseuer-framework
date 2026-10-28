using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Crafting;
using ObenFramework.Items;
using ObenFramework.Player;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>World objects that give, grow or take items, listed by kind, and their events.</summary>
internal static class WorldItemCommands
{
    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        ResourceNodes.Hit.Subscribe(owner, e => recorder.Add("NodeHit", new JObject { ["id"] = e.NodeId, ["stock"] = e.Stock, ["yielded"] = e.Yielded }));
        ResourceNodes.Depleted.Subscribe(owner, id => recorder.Add("NodeDepleted", new JObject { ["id"] = id }));
        Spawners.Spawned.Subscribe(owner, s => recorder.Add("Spawned", new JObject { ["id"] = s.Id, ["kind"] = s.Kind, ["spawned"] = s.Spawned }));
        Breakables.Broken.Subscribe(owner, id => recorder.Add("Broken", new JObject { ["id"] = id }));
        Breakables.Repaired.Subscribe(owner, id => recorder.Add("Repaired", new JObject { ["id"] = id }));
        Breakables.LockBroken.Subscribe(owner, id => recorder.Add("LockBroken", new JObject { ["id"] = id }));
        Breakables.Smashed.Subscribe(owner, s => recorder.Add("Smashed", new JObject { ["id"] = s.Id, ["animal"] = s.Animal, ["itemId"] = s.ItemId }));
        Extractables.Extracted.Subscribe(owner, id => recorder.Add("Extracted", new JObject { ["id"] = id }));
        ItemGivers.Gave.Subscribe(owner, g => recorder.Add("Gave", new JObject { ["id"] = g.GiverId, ["itemId"] = g.ItemId, ["amount"] = g.Amount }));
        LootPools.ContainerFilled.Subscribe(owner, f => recorder.Add("ContainerFilled", new JObject { ["container"] = f.Container.Id, ["items"] = new JArray(f.ItemIds.ToArray()) }));
        UnloadedStorage.Applied.Subscribe(owner, c => recorder.Add("UnloadedApplied", new JObject { ["scene"] = c.Scene, ["container"] = c.Guid, ["itemId"] = c.ItemId, ["amount"] = c.Amount, ["give"] = c.Give }));
        UnloadedStorage.Failed.Subscribe(owner, c => recorder.Add("UnloadedFailed", new JObject { ["scene"] = c.Scene, ["container"] = c.Guid, ["reason"] = c.Reason }));
    });

    private static readonly string[] Kinds =
        { "nodes", "spawners", "breakables", "locks", "extractables", "givers", "taskitems", "plants", "growing", "autofarms", "dishwashers", "recyclers", "upgrades" };

    [BridgeCommand("worlditems.list", "World objects that give, grow or take items, of one kind, nearest first.",
        Params = new[] { "kind: nodes, spawners, breakables, locks, extractables, givers, taskitems, plants, growing, autofarms, dishwashers, recyclers or upgrades",
            "radius?: only within this many metres of the player", "max?: default 30" })]
    private static object List(JObject p)
    {
        string kind = p.Str("kind");
        var rows = Rows(kind) ?? throw new ArgumentException("kind must be one of: " + string.Join(", ", Kinds));
        float radius = p.Float("radius", 0f);
        int max = Math.Max(1, p.Int("max", 30));
        var origin = PlayerRef.Position;

        var entries = new List<(float distance, JObject row)>();
        foreach (var (position, row) in rows)
        {
            float distance = origin.HasValue && position.HasValue ? Vector3.Distance(origin.Value, position.Value) : 0f;
            if (radius > 0f && origin.HasValue && position.HasValue && distance > radius)
                continue;
            if (origin.HasValue && position.HasValue)
                row["distance"] = Math.Round(distance, 2);
            entries.Add((distance, row));
        }
        entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        for (int i = 0; i < entries.Count && i < max; i++)
            list.Add(entries[i].row);
        return new JObject { ["kind"] = kind, ["total"] = entries.Count, ["items"] = list };
    }

    [BridgeCommand("worlditems.events", "Events of resource nodes, spawners, breakables, extractables, item givers, loot fills and queued container changes since recording started.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object EventsList(JObject p) => Events.Read(p.Bool("clear", true));

    private static List<(Vector3? position, JObject row)> Rows(string kind)
    {
        var rows = new List<(Vector3?, JObject)>();
        switch (kind)
        {
            case "nodes":
                foreach (var n in ResourceNodes.All())
                    rows.Add((n.Position, new JObject { ["id"] = n.Id, ["pos"] = ItemJson.Vec(n.Position), ["stock"] = $"{n.Stock}/{n.MaxStock}", ["items"] = new JArray(n.ItemIds.ToArray()), ["respawns"] = n.Respawns }));
                break;
            case "spawners":
                foreach (var s in Spawners.All())
                    rows.Add((s.Position, new JObject { ["id"] = s.Id, ["kind"] = s.Kind, ["pos"] = ItemJson.Vec(s.Position), ["respawns"] = s.Respawns, ["chance"] = s.Chance, ["spawned"] = s.Spawned, ["time"] = s.SpawnTime.ToString() }));
                break;
            case "breakables":
                foreach (var b in Breakables.All())
                    rows.Add((b.Position, new JObject { ["id"] = b.Id, ["name"] = b.Name, ["pos"] = ItemJson.Vec(b.Position), ["broken"] = b.Broken, ["repairs"] = b.Repairs }));
                break;
            case "locks":
                foreach (var l in Breakables.Locks())
                    rows.Add((l.Position, new JObject { ["id"] = l.Id, ["pos"] = ItemJson.Vec(l.Position), ["locked"] = l.Locked, ["strength"] = l.Strength }));
                break;
            case "extractables":
                foreach (var x in Extractables.All())
                    rows.Add((x.Position, new JObject { ["id"] = x.Id, ["name"] = x.Name, ["pos"] = ItemJson.Vec(x.Position), ["health"] = x.Health }));
                break;
            case "givers":
                foreach (var g in ItemGivers.All())
                    rows.Add((g.Position, new JObject { ["id"] = g.Id, ["title"] = g.Title, ["pos"] = ItemJson.Vec(g.Position), ["canGive"] = g.CanGive, ["amount"] = g.Amount, ["empty"] = g.Empty }));
                break;
            case "taskitems":
                foreach (var t in TaskItems.All())
                    rows.Add((null, new JObject { ["id"] = t.Id, ["name"] = t.Name, ["type"] = t.Type, ["amount"] = t.Amount }));
                break;
            case "plants":
                foreach (var pl in Plants.All())
                    rows.Add((pl.Position, new JObject { ["id"] = pl.Id, ["pos"] = ItemJson.Vec(pl.Position), ["age"] = $"{pl.Age}/{pl.FullGrownAge}", ["stage"] = Math.Round(pl.StageProgress, 2), ["paused"] = pl.Paused, ["rotting"] = pl.Rotting, ["readyFruit"] = pl.ReadyFruit }));
                break;
            case "growing":
                foreach (var g in GrowingStations.All())
                    rows.Add((g.Position, new JObject { ["id"] = g.Id, ["pos"] = ItemJson.Vec(g.Position), ["spots"] = g.Spots, ["planted"] = g.Planted.Count, ["paused"] = g.Paused, ["manualHarvest"] = g.ManualHarvest }));
                break;
            case "autofarms":
                foreach (var a in Autofarms.All())
                    rows.Add((a.Position, new JObject { ["id"] = a.Id, ["pos"] = ItemJson.Vec(a.Position), ["planted"] = $"{a.Planted}/{a.Slots}", ["plants"] = new JArray(a.PlantItemIds.ToArray()), ["grow"] = $"{a.GrowPercentMin}-{a.GrowPercentMax}%" }));
                break;
            case "dishwashers":
                foreach (var d in Dishwashers.All())
                    rows.Add((d.Position, new JObject { ["id"] = d.Id, ["pos"] = ItemJson.Vec(d.Position), ["busy"] = d.Busy, ["powered"] = d.Powered, ["progress"] = Math.Round(d.Progress, 2) }));
                break;
            case "recyclers":
                foreach (var r in Recycling.All())
                    rows.Add((r.Position, new JObject { ["id"] = r.Id, ["pos"] = ItemJson.Vec(r.Position), ["owed"] = r.Owed }));
                break;
            case "upgrades":
                foreach (var u in CraftingUpgrades.All())
                    rows.Add((u.Position, new JObject { ["id"] = u.Id, ["name"] = u.Name, ["pos"] = ItemJson.Vec(u.Position), ["on"] = u.On, ["quality"] = u.EquipmentQuality, ["recipes"] = new JArray(u.Recipes), ["connected"] = new JArray(u.Connected) }));
                break;
            default:
                return null;
        }
        return rows;
    }
}
