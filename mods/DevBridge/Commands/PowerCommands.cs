using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;
using ObenFramework.Player;
using ObenFramework.World;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Power devices and grids, through ObenFramework.Env.PowerDevices and ObenFramework.World.PowerGrids.</summary>
internal static class PowerCommands
{
    [BridgeCommand("power.devices", "Appliances, generators, grid consumers and batteries of the loaded scenes, nearest to the player first.",
        Params = new[] { "kind?: appliance, generator, consumer or battery", "radius?: metres around the player", "max?: default 50" })]
    private static object Devices(JObject p)
    {
        string kind = p.Str("kind", null);
        float radius = p.Float("radius", 0f);
        int max = Math.Max(1, p.Int("max", 50));
        var origin = PlayerRef.Position;

        var found = PowerDevices.All();
        found.RemoveAll(d => (kind != null && !string.Equals(PowerDevices.KindOf(d)?.ToString(), kind, StringComparison.OrdinalIgnoreCase))
            || (radius > 0f && origin.HasValue && Vector3.Distance(origin.Value, d.transform.position) > radius));
        if (origin.HasValue)
            found.Sort((a, b) => Vector3.Distance(origin.Value, a.transform.position).CompareTo(Vector3.Distance(origin.Value, b.transform.position)));

        var list = new JArray();
        for (int i = 0; i < found.Count && i < max; i++)
            list.Add(Describe(PowerDevices.Describe(found[i])));
        return new JObject { ["total"] = found.Count, ["devices"] = list };
    }

    [BridgeCommand("power.set", "Switch a power device on or off by id (see power.devices).", Params = new[] { "id: device id", "on: true or false" })]
    private static object Set(JObject p)
    {
        string id = p.Str("id");
        var device = PowerDevices.Find(id) ?? throw new ArgumentException($"No power device '{id}'.");
        bool accepted = PowerDevices.SetOn(device, p.Bool("on"));
        var result = Describe(PowerDevices.Describe(device));
        result["accepted"] = accepted;
        return result;
    }

    [BridgeCommand("power.grids", "Power grids of the loaded scenes with their numbers.")]
    private static object Grids(JObject p)
    {
        var list = new JArray();
        foreach (var grid in PowerGrids.All())
            list.Add(Grid(PowerGrids.Describe(grid)));
        return new JObject { ["grids"] = list };
    }

    [BridgeCommand("power.grid", "Switch a grid's breaker or set its base capacity.", Params = new[] { "id: grid id (see power.grids)", "enabled?: true or false", "capacity?: base watts" })]
    private static object GridSet(JObject p)
    {
        string id = p.Str("id");
        var grid = PowerGrids.Find(id) ?? throw new ArgumentException($"No power grid '{id}'.");
        if (p["enabled"] == null && p["capacity"] == null)
            throw new ArgumentException("Give enabled and/or capacity.");
        if (p["enabled"] != null)
            PowerGrids.SetEnabled(grid, p.Bool("enabled"));
        if (p["capacity"] != null)
            PowerGrids.SetBaseCapacity(grid, p.Int("capacity", 0));
        return Grid(PowerGrids.Describe(grid));
    }

    private static JObject Describe(PowerDeviceInfo d)
    {
        var o = new JObject
        {
            ["id"] = d.Id, ["name"] = d.Name, ["kind"] = d.Kind.ToString(), ["on"] = d.IsOn, ["running"] = d.IsRunning,
            ["watts"] = d.Watts, ["grid"] = d.GridId,
        };
        if (d.AlwaysOn)
            o["alwaysOn"] = true;
        if (d.Broken)
            o["broken"] = true;
        if (d.RequiresFuel)
        {
            o["fuelSeconds"] = d.FuelSeconds;
            o["burning"] = d.Burning;
        }
        if (d.Kind == PowerDeviceKind.Battery)
        {
            o["charge"] = d.Charge;
            o["maxCharge"] = d.MaxCharge;
        }
        return o;
    }

    private static JObject Grid(PowerGridInfo g) => new JObject
    {
        ["id"] = g.Id, ["name"] = g.Name, ["enabled"] = g.Enabled, ["baseCapacity"] = g.BaseCapacity, ["localBalance"] = g.LocalBalance,
        ["netBalance"] = g.NetBalance, ["totalCapacity"] = g.TotalCapacity, ["freeCapacity"] = g.FreeCapacity, ["unlimited"] = g.Unlimited, ["parent"] = g.ParentId,
    };
}
