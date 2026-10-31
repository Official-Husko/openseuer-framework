using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;

namespace DevBridge.Commands;

/// <summary>Lanterns, tanks, flood gates, showers, saunas and lights with several states, through ObenFramework.Env.</summary>
internal static class FixtureCommands
{
    private const string Kinds = "lantern, tank, floodgate, shower, sauna or light";

    [BridgeCommand("fixtures.list", "Fixtures of one kind in the loaded scenes.", Params = new[] { "kind: " + Kinds })]
    private static object List(JObject p)
    {
        var list = new JArray();
        switch (p.Str("kind").ToLowerInvariant())
        {
            case "lantern":
                foreach (var l in Lanterns.All())
                    list.Add(new JObject { ["id"] = l.Id, ["name"] = l.Name, ["lit"] = l.Lit, ["secondsLeft"] = l.SecondsLeft });
                break;
            case "tank":
                foreach (var t in Tanks.All())
                    list.Add(Tank(t));
                break;
            case "floodgate":
                foreach (var g in FloodGates.All())
                    list.Add(new JObject { ["id"] = g.Id, ["name"] = g.Name, ["open"] = g.Open, ["moving"] = g.Moving, ["powered"] = g.Powered });
                break;
            case "shower":
                foreach (var s in Showers.All())
                    list.Add(new JObject { ["id"] = s.Id, ["name"] = s.Name, ["on"] = s.On, ["running"] = s.Running });
                break;
            case "sauna":
                foreach (var s in Saunas.All())
                    list.Add(Sauna(s));
                break;
            case "light":
                foreach (var l in MultiStateLights.All())
                    list.Add(new JObject { ["id"] = l.Id, ["name"] = l.Name, ["on"] = l.On, ["state"] = l.State, ["states"] = l.StateCount });
                break;
            default:
                throw new ArgumentException("kind is " + Kinds + ".");
        }
        return new JObject { ["count"] = list.Count, ["fixtures"] = list };
    }

    [BridgeCommand("fixtures.set", "Switch or fill a fixture by id (see fixtures.list).",
        Params = new[]
        {
            "kind: " + Kinds, "id: fixture id",
            "on?: lantern lit, shower or sauna on, light on, flood gate powered", "open?: flood gate", "state?: light state, -1 for none",
            "item?/amount?: tank liquid item and litres (amount 0 empties)", "locked?: tank lock", "water?: true throws water in a sauna",
        })]
    private static object Set(JObject p)
    {
        string id = p.Str("id");
        switch (p.Str("kind").ToLowerInvariant())
        {
            case "lantern":
            {
                var lantern = Lanterns.Find(id) ?? throw Missing(id);
                bool ok = p.Bool("on") ? Lanterns.Light(lantern) : Lanterns.Extinguish(lantern);
                var l = Lanterns.Describe(lantern);
                return new JObject { ["ok"] = ok, ["id"] = l.Id, ["lit"] = l.Lit, ["secondsLeft"] = l.SecondsLeft };
            }
            case "tank":
            {
                var tank = Tanks.Find(id) ?? throw Missing(id);
                if (p["locked"] != null && tank._Lock == null)
                    throw new InvalidOperationException("That container has no lock.");
                bool ok = true;
                if (p["amount"] != null)
                    ok &= p.Float("amount") <= 0f ? Tanks.Empty(tank) : Tanks.Fill(tank, p.Int("item", tank.Liquid != null ? tank.Liquid.ID : 0), p.Float("amount"));
                if (p["locked"] != null)
                    ok &= Tanks.SetLocked(tank, p.Bool("locked"));
                var result = Tank(Tanks.Describe(tank));
                result["ok"] = ok;
                return result;
            }
            case "floodgate":
            {
                var gate = FloodGates.Find(id) ?? throw Missing(id);
                bool accepted = p["on"] == null || FloodGates.SetPowered(gate, p.Bool("on"));
                accepted &= p["open"] == null || FloodGates.SetOpen(gate, p.Bool("open"));
                var g = FloodGates.Describe(gate);
                return new JObject { ["id"] = g.Id, ["open"] = g.Open, ["moving"] = g.Moving, ["powered"] = g.Powered, ["accepted"] = accepted };
            }
            case "shower":
            {
                var shower = Showers.Find(id) ?? throw Missing(id);
                bool ok = Showers.SetOn(shower, p.Bool("on"));
                var s = Showers.Describe(shower);
                return new JObject { ["ok"] = ok, ["id"] = s.Id, ["on"] = s.On, ["running"] = s.Running };
            }
            case "sauna":
            {
                var sauna = Saunas.Find(id) ?? throw Missing(id);
                bool ok = p["on"] == null || Saunas.SetOn(sauna, p.Bool("on"));
                if (p.Bool("water", false))
                    ok &= Saunas.ThrowWater(sauna);
                var result = Sauna(Saunas.Describe(sauna));
                result["ok"] = ok;
                return result;
            }
            case "light":
            {
                var light = MultiStateLights.Find(id) ?? throw Missing(id);
                bool ok = p["state"] == null || MultiStateLights.SetState(light, p.Int("state", -1));
                if (p["on"] != null)
                    ok &= MultiStateLights.SetOn(light, p.Bool("on"));
                var l = MultiStateLights.Describe(light);
                return new JObject { ["ok"] = ok, ["id"] = l.Id, ["on"] = l.On, ["state"] = l.State, ["states"] = l.StateCount };
            }
            default:
                throw new ArgumentException("kind is " + Kinds + ".");
        }
    }

    private static JObject Tank(TankInfo t) => new JObject
    {
        ["id"] = t.Id, ["name"] = t.Name, ["liquid"] = t.LiquidItemId, ["amount"] = t.Amount, ["capacity"] = t.Capacity,
        ["locked"] = t.Locked, ["supply"] = t.IsSupply,
    };

    private static JObject Sauna(SaunaInfo s) => new JObject
    {
        ["id"] = s.Id, ["name"] = s.Name, ["on"] = s.On, ["temperature"] = s.Temperature, ["steam"] = s.Steam, ["passive"] = s.Passive,
    };

    private static ArgumentException Missing(string id) => new ArgumentException($"No fixture '{id}' of that kind.");
}
