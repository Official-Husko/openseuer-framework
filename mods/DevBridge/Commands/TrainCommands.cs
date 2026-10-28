using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Transport;

namespace DevBridge.Commands;

/// <summary>Trains, the minitrain, track switches, ticket machines, drones and wait stations.</summary>
internal static class TrainCommands
{
    private static readonly Dictionary<string, IDisposable> Prices = new Dictionary<string, IDisposable>();

    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        Trains.Moved.Subscribe(owner, e => recorder.Add("Train" + e.Kind, new JObject { ["id"] = e.Id, ["speed"] = Math.Round(e.Speed, 2), ["other"] = e.OtherId }));
        TrainTickets.Changed.Subscribe(owner, e => recorder.Add(e.Bought ? "TicketBought" : "TicketTaken", new JObject
        {
            ["machine"] = e.Id, ["price"] = e.Price, ["currency"] = e.Currency.ToString(), ["item"] = e.TicketItemId,
        }));
        Drones.Changed.Subscribe(owner, e => recorder.Add(e.Node != null ? "DroneNode" : "DroneSwitched", new JObject
        {
            ["id"] = e.Id, ["node"] = Drones.IdOf(e.Node), ["active"] = e.Active, ["routeEnd"] = e.RouteEnd,
        }));
        WaitStations.Finished.Subscribe(owner, e => recorder.Add("WaitStationDone", new JObject { ["id"] = e.Id, ["seconds"] = e.Seconds }));
    });

    [BridgeCommand("trains.list", "Rail vehicles of the level, the minitrain, and with segments=true the track segments and switches.",
        Params = new[] { "segments?: also list segments and switches, default false" })]
    private static object TrainsList(JObject p)
    {
        var list = new JArray();
        foreach (var train in Trains.All())
        {
            var info = Trains.Describe(train);
            list.Add(new JObject
            {
                ["id"] = info.Id, ["name"] = train.name, ["speed"] = Math.Round(info.Speed, 2), ["target"] = Math.Round(info.TargetSpeed, 2),
                ["engine"] = info.EngineOn, ["braking"] = info.Braking, ["front"] = info.FrontCoupledTo, ["back"] = info.BackCoupledTo,
                ["wagon"] = info.WagonType, ["cargo"] = info.CargoId, ["minitrain"] = info.IsMinitrain, ["pos"] = ItemJson.Vec(info.Position),
            });
        }
        var cab = Trains.Minitrain;
        var result = new JObject
        {
            ["trains"] = list,
            ["minitrain"] = cab.HasValue ? new JObject
            {
                ["fuel"] = Math.Round(cab.Value.Fuel, 2), ["capacity"] = Math.Round(cab.Value.Capacity, 2), ["running"] = cab.Value.Running,
                ["playerDriving"] = cab.Value.PlayerDriving, ["speed"] = Math.Round(cab.Value.Speed, 2),
            } : null,
        };
        if (p.Bool("segments", false))
        {
            var segments = new JArray();
            foreach (var s in Trains.Segments())
                segments.Add(new JObject { ["id"] = s.Id, ["occupied"] = s.Occupied, ["by"] = s.OccupiedBy, ["branch"] = s.ActiveBranch, ["previous"] = s.Previous, ["next"] = new JArray(s.Next) });
            var switches = new JArray();
            foreach (var s in Trains.Switches())
                switches.Add(new JObject { ["id"] = Trains.IdOf(s), ["branch"] = s.currentBranchIndex, ["branches"] = Trains.BranchCount(s), ["pos"] = ItemJson.Vec(s.transform.position) });
            result["segments"] = segments;
            result["switches"] = switches;
        }
        return result;
    }

    [BridgeCommand("trains.drive", "Engine, target speed and brakes of a vehicle (each optional).",
        Params = new[] { "id: train GUID from trains.list", "engine?: true or false", "speed?: target speed", "brake?: true or false" })]
    private static object TrainsDrive(JObject p)
    {
        var train = Trains.Find(p.Str("id"));
        if (train == null)
            throw new ArgumentException($"No train '{p.Str("id")}'.");
        if (p["engine"] != null)
            TrainActions.SetEngine(train, p.Bool("engine"));
        if (p["speed"] != null)
            TrainActions.SetTargetSpeed(train, p.Float("speed"));
        if (p["brake"] != null)
            TrainActions.Brake(train, p.Bool("brake"));
        var info = Trains.Describe(train);
        return new JObject { ["engine"] = info.EngineOn, ["target"] = Math.Round(info.TargetSpeed, 2), ["braking"] = info.Braking };
    }

    [BridgeCommand("trains.switch", "Throws a track switch to a branch.", Params = new[] { "id: switch id", "branch: index", "force?: even with a train on it" })]
    private static object TrainsSwitch(JObject p)
    {
        var trackSwitch = Trains.FindSwitch(p.Str("id"));
        if (trackSwitch == null)
            throw new ArgumentException($"No switch '{p.Str("id")}'.");
        return new JObject { ["ok"] = Trains.SetBranch(trackSwitch, p.Int("branch", 0), p.Bool("force", false)) };
    }

    [BridgeCommand("tickets.list", "Ticket machines with their prices. 'price' or 'factor' adds a rule owned by the bridge (for 'id' or all), 'reset' drops the bridge's rules.",
        Params = new[] { "id?: one machine", "price?: fixed price", "factor?: price factor", "reset?: true" })]
    private static object TicketsList(JObject p)
    {
        string id = p.Str("id", null);
        if (p.Bool("reset", false))
        {
            foreach (var rule in Prices.Values)
                rule.Dispose();
            Prices.Clear();
        }
        else if (p["price"] != null || p["factor"] != null)
        {
            string key = id ?? "*";
            if (Prices.TryGetValue(key, out var old))
                old.Dispose();
            Prices[key] = TrainTickets.AddRule(DevBridgeMod.Instance, new TicketPriceRule
            {
                Machine = id, Price = p["price"] != null ? p.Int("price", 0) : (int?)null, Factor = p.Float("factor", 1f),
            });
        }

        var list = new JArray();
        foreach (var machine in TrainTickets.All())
        {
            var info = TrainTickets.Describe(machine);
            list.Add(new JObject { ["id"] = info.Id, ["price"] = info.Price, ["base"] = info.BasePrice, ["currency"] = info.Currency.ToString(), ["ticket"] = info.TicketItemId });
        }
        return new JObject { ["machines"] = list };
    }

    [BridgeCommand("drones.list", "Drones with their route, and with nodes=true the air nodes.", Params = new[] { "nodes?: default false" })]
    private static object DronesList(JObject p)
    {
        var list = new JArray();
        foreach (var drone in Drones.All())
        {
            var info = Drones.Describe(drone);
            list.Add(new JObject { ["id"] = info.Id, ["active"] = info.Active, ["maxSpeed"] = Math.Round(info.MaxSpeed, 2), ["target"] = info.TargetNode, ["next"] = info.NextNode, ["pos"] = ItemJson.Vec(info.Position) });
        }
        var result = new JObject { ["drones"] = list };
        if (p.Bool("nodes", false))
        {
            var nodes = new JArray();
            foreach (var node in Drones.Nodes())
                nodes.Add(new JObject { ["id"] = Drones.IdOf(node), ["next"] = Drones.IdOf(node.NextNode), ["pos"] = ItemJson.Vec(node.transform.position) });
            result["nodes"] = nodes;
        }
        return result;
    }

    [BridgeCommand("drones.set", "Switches a drone, sends it to a node or changes its speed.",
        Params = new[] { "id: drone id", "active?: true or false", "target?: air node id", "speed?: max speed" })]
    private static object DronesSet(JObject p)
    {
        var drone = Drones.Find(p.Str("id"));
        if (drone == null)
            throw new ArgumentException($"No drone '{p.Str("id")}'.");
        string target = p.Str("target", null);
        if (target != null)
        {
            var node = Drones.FindNode(target);
            if (node == null)
                throw new ArgumentException($"No air node '{target}'.");
            Drones.SetTarget(drone, node);
        }
        if (p["speed"] != null)
            Drones.SetMaxSpeed(drone, p.Float("speed"));
        if (p["active"] != null)
            Drones.SetActive(drone, p.Bool("active"));
        var info = Drones.Describe(drone);
        return new JObject { ["active"] = info.Active, ["target"] = info.TargetNode, ["maxSpeed"] = Math.Round(info.MaxSpeed, 2) };
    }

    [BridgeCommand("waits.list", "Wait stations (benches, desks, TVs, jobs) of the level and whether the player can use them now.")]
    private static object WaitsList(JObject p)
    {
        var list = new JArray();
        foreach (var station in WaitStations.All())
        {
            var info = WaitStations.Describe(station);
            WaitStations.CanUse(station, out string reason);
            list.Add(new JObject
            {
                ["id"] = info.Id, ["title"] = station.title, ["kind"] = info.Kind.ToString(), ["minutes"] = info.Minutes, ["ignoresNeeds"] = info.IgnoresNeeds,
                ["canUse"] = reason == null, ["reason"] = reason, ["pos"] = ItemJson.Vec(station.transform.position),
            });
        }
        return new JObject { ["stations"] = list };
    }

    [BridgeCommand("waits.use", "Uses a wait station as the player would (time passes).", Params = new[] { "id: station id" })]
    private static object WaitsUse(JObject p)
    {
        var station = WaitStations.Find(p.Str("id"));
        if (station == null)
            throw new ArgumentException($"No wait station '{p.Str("id")}'.");
        bool ok = WaitStations.Use(station);
        WaitStations.CanUse(station, out string reason);
        return new JObject { ["ok"] = ok, ["reason"] = ok ? null : reason };
    }

    [BridgeCommand("trains.events", "Train, ticket, drone and wait station events since recording started.", Params = new[] { "clear?: default true" })]
    private static object TrainsEvents(JObject p) => Events.Read(p.Bool("clear", true));
}
