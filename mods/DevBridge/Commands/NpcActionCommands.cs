using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Npcs;
using ObenFramework.Player;

namespace DevBridge.Commands;

/// <summary>One-off NPC actions, relationships, the police and surveillance, and the phones in the scene.</summary>
internal static class NpcActionCommands
{
    [BridgeCommand("npcs.act", "Does one thing to an NPC in the scene (they change the game, like playing would).",
        Params = new[] { "id? or name?: the NPC", "action: teleport, kill, hit, follow, unfollow, say, comment, talk, lookAtPlayer, stopLooking, changeLevel",
            "x?,y?,z? or pos?: for teleport", "text?: for say; conversation title for comment and talk", "run?: for follow", "seconds?: for lookAtPlayer", "scene?: for changeLevel" })]
    private static object Act(JObject p)
    {
        var npc = p["id"] != null ? Npcs.Get(p.Int("id", -1)) : Npcs.Find(p.Str("name"));
        if (npc == null)
            throw new ArgumentException("No such NPC.");
        int id = npc.Id;
        bool ok;
        switch (p.Str("action").ToLowerInvariant())
        {
            case "teleport": ok = NpcActions.Teleport(id, p.Position() ?? PlayerRef.Position ?? throw new ArgumentException("Give a position.")); break;
            case "kill": ok = NpcActions.Kill(id); break;
            case "hit": ok = NpcActions.Hit(id); break;
            case "follow": ok = NpcActions.FollowPlayer(id, p.Bool("run", false)); break;
            case "unfollow": ok = NpcActions.StopFollowing(id); break;
            case "say": ok = NpcActions.Say(id, p.Str("text")); break;
            case "comment": ok = NpcActions.Comment(id, p.Str("text")); break;
            case "talk": ok = NpcActions.StartConversation(id, p.Str("text")); break;
            case "lookatplayer": ok = NpcActions.LookAtPlayer(id, p.Int("seconds", 0)); break;
            case "stoplooking": ok = NpcActions.StopLooking(id); break;
            case "changelevel": ok = NpcActions.ChangeLevel(id, p.Str("scene")); break;
            default: throw new ArgumentException("Unknown action.");
        }
        return new JObject { ["ok"] = ok, ["npc"] = npc.FullName, ["id"] = id };
    }

    [BridgeCommand("npcs.relationship", "The player's relationships; with id and value, sets one's disposition (saved with the game).",
        Params = new[] { "id?: relationship id or name", "value?: new disposition, -100 to 100", "target?: where it drifts to, default the value", "active?: pause (false) or resume (true)" })]
    private static object Relationship(JObject p)
    {
        string id = p.Str("id", null);
        var result = new JObject();
        if (id != null && p["value"] != null)
            result["set"] = Relationships.SetDisposition(id, p.Float("value"), p["target"] != null ? p.Float("target") : (float?)null);
        if (id != null && p["active"] != null)
            result["activeSet"] = Relationships.SetActive(id, p.Bool("active"));

        var list = new JArray();
        foreach (var r in Relationships.All())
        {
            if (id != null && r.Id != id && !string.Equals(r.Name, id, StringComparison.OrdinalIgnoreCase))
                continue;
            list.Add(new JObject
            {
                ["id"] = r.Id, ["name"] = r.Name, ["disposition"] = ModNpcCommands.Round(r.Disposition),
                ["target"] = ModNpcCommands.Round(r.Target), ["perDay"] = r.ChangePerDay, ["active"] = r.Active,
            });
        }
        result["relationships"] = list;
        return result;
    }

    [BridgeCommand("npcs.crime", "Police, robbers, cameras, trespassing and janitor tags in the scene; can call or stop the police.",
        Params = new[] { "action?: callPolice, chase (a severe call) or stopPursuits", "anyDistance?: call officers at any distance, default false" })]
    private static object Crime(JObject p)
    {
        var result = new JObject();
        switch (p.Str("action", string.Empty).ToLowerInvariant())
        {
            case "": break;
            case "callpolice": result["called"] = Pursuit.CallPolice(false, p.Bool("anyDistance", false)); break;
            case "chase": result["called"] = Pursuit.CallPolice(true, p.Bool("anyDistance", false)); break;
            case "stoppursuits": result["stopped"] = Pursuit.StopAll(); break;
            default: throw new ArgumentException("action is callPolice, chase or stopPursuits.");
        }

        result["pursued"] = Pursuit.IsPursued;
        result["police"] = new JArray(Pursuit.ActivePolice());
        var robbers = new JArray();
        foreach (var r in Robbers.All())
            robbers.Add(new JObject { ["id"] = r.NpcId, ["state"] = r.State, ["loaded"] = r.Loaded });
        result["robbers"] = robbers;
        result["trespassing"] = Surveillance.IsTrespassing;
        result["seen"] = Surveillance.IsPlayerSeen();
        var cameras = new JArray();
        foreach (var c in Surveillance.Cameras())
            cameras.Add(new JObject { ["address"] = c.Address, ["on"] = c.On, ["seesPlayer"] = c.SeesPlayer, ["range"] = c.Range, ["fov"] = c.FieldOfView });
        result["cameras"] = cameras;
        result["janitorTags"] = JObject.FromObject(Janitors.Tags());
        return result;
    }

    [BridgeCommand("phone.scene", "The phones in the scene: address, network, in a call, ringing and who calls.",
        Params = new[] { "ring?: a phone address to ring", "number?: the number that calls (with ring)", "seconds?: how long it rings, default 30" })]
    private static object PhonesInScene(JObject p)
    {
        var result = new JObject();
        if (p["ring"] != null)
            result["ringing"] = Phones.Ring(p.Str("ring"), p.Str("number"), p.Int("seconds", 30));
        var list = new JArray();
        foreach (var phone in Phones.All())
        {
            list.Add(new JObject
            {
                ["address"] = phone.Address, ["name"] = phone.Name, ["pos"] = ModNpcCommands.Vec(phone.Position), ["network"] = phone.Network.ToString(),
                ["inCall"] = phone.InCall, ["ringing"] = phone.Ringing, ["caller"] = phone.Caller, ["ownerNpc"] = phone.OwnerNpc,
            });
        }
        result["phones"] = list;
        return result;
    }
}
