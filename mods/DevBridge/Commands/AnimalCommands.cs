using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Animals;
using ObenFramework.Player;

namespace DevBridge.Commands;

/// <summary>Animals, breeding pens and rat traps through ObenFramework.Animals.</summary>
internal static class AnimalCommands
{
    [BridgeCommand("animals.list", "The animals in the scene with kind, state, tame, health, hunger and age.",
        Params = new[] { "kind?: cat, chick, chicken, pig, rat or other", "max?: default 100" })]
    private static object List(JObject p)
    {
        AnimalKind? kind = p["kind"] != null ? (AnimalKind)Enum.Parse(typeof(AnimalKind), p.Str("kind"), true) : (AnimalKind?)null;
        int max = Math.Max(1, p.Int("max", 100));
        var list = new JArray();
        int total = 0;
        foreach (var a in Animals.All())
        {
            if (kind.HasValue && a.Kind != kind.Value)
                continue;
            total++;
            if (list.Count < max)
                list.Add(Describe(a));
        }
        return new JObject { ["total"] = total, ["counted"] = Animals.Count, ["limit"] = Animals.Limit, ["animals"] = list };
    }

    [BridgeCommand("animals.act", "Makes an animal do something (they change the game, like playing would).",
        Params = new[] { "id: animal id from animals.list", "action: sit, sleep, sleepHold, wake, run, hunt (the player), stopHunt, tame, wild, damage",
            "seconds?: for run, default 5", "amount?: for damage, default 1" })]
    private static object Act(JObject p)
    {
        int id = p.Int("id", 0);
        var bridge = DevBridgeMod.Instance;
        bool ok;
        switch (p.Str("action").ToLowerInvariant())
        {
            case "sit": ok = AnimalActions.Sit(id); break;
            case "sleep": ok = AnimalActions.Sleep(id); break;
            case "sleephold": ok = AnimalActions.SleepUntilWoken(bridge, id); break;
            case "wake": ok = AnimalActions.Wake(bridge, id); break;
            case "run": ok = AnimalActions.Run(id, p.Float("seconds", 5f)); break;
            case "hunt": ok = AnimalActions.Hunt(bridge, id, PlayerRef.Body); break;
            case "stophunt": ok = AnimalActions.StopHunt(id); break;
            case "tame": ok = AnimalTames.SetTame(bridge, id, true); break;
            case "wild": ok = AnimalTames.SetTame(bridge, id, false); break;
            case "damage": ok = Animals.Damage(id, p.Float("amount", 1f)); break;
            default: throw new ArgumentException("Unknown action.");
        }
        var animal = Animals.Get(id);
        return new JObject { ["ok"] = ok, ["animal"] = animal != null ? Describe(animal) : null };
    }

    [BridgeCommand("animals.breeders", "The breeding pens in the scene: animals in them, places and chance per hour.")]
    private static object Breeders(JObject p)
    {
        var list = new JArray();
        foreach (var b in Breeding.Breeders())
        {
            list.Add(new JObject
            {
                ["address"] = b.Address, ["pos"] = ModNpcCommands.Vec(b.Position), ["animals"] = b.Animals, ["places"] = b.Places,
                ["chancePerHour"] = ModNpcCommands.Round(b.ChancePerHour),
            });
        }
        return new JObject { ["breeders"] = list };
    }

    private static JObject Describe(AnimalRecord a) => new JObject
    {
        ["id"] = a.Id,
        ["kind"] = a.Kind.ToString(),
        ["name"] = a.Name,
        ["guid"] = a.Guid,
        ["pos"] = ModNpcCommands.Vec(a.Position),
        ["state"] = a.State.ToString(),
        ["tame"] = a.Tame,
        ["following"] = a.Following,
        ["running"] = a.Running,
        ["health"] = ModNpcCommands.Round(a.Health),
        ["maxHealth"] = a.MaxHealth,
        ["hunger"] = ModNpcCommands.Round(a.Hunger),
        ["age"] = ModNpcCommands.Round(a.Age),
        ["fullGrownAge"] = a.FullGrownAge,
    };
}
