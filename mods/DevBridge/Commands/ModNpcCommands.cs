using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Npcs;
using ObenFramework.Player;
using ObenFramework.Player.Look;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Mod NPCs through ObenFramework.Npcs.ModNpcs. NPCs spawned here belong to DevBridge and go when it reloads.</summary>
internal static class ModNpcCommands
{
    [BridgeCommand("npcs.spawn", "Spawns a mod NPC (owned by DevBridge) copied from a game character, by default two metres in front of the player.",
        Params = new[] { "name: the NPC's name within DevBridge", "template: character id to copy (see npcs.characters)", "body?: character id of another body",
            "first?: first name, default the name", "last?: last name", "x?,y?,z? or pos?: where, default in front of the player", "yaw?: facing in degrees, default towards the player",
            "saved?: keep it in the save, default false", "dialogue?: keep the template's conversation, default false", "look?: a look JSON object (see player.look)" })]
    private static object Spawn(JObject p)
    {
        string name = p.Str("name");
        var spot = Spot(p);
        var spec = new ModNpcSpec
        {
            Template = p.Int("template", 0),
            Body = p.Int("body", 0),
            FirstName = p.Str("first", name),
            LastName = p.Str("last", string.Empty),
            Position = spot.position,
            Yaw = p.Float("yaw", spot.yaw),
            Saved = p.Bool("saved", false),
            KeepDialogue = p.Bool("dialogue", false),
            Look = p["look"] is JObject look ? LookData.FromJson(look.ToString()) : null,
        };
        var npc = ModNpcs.Spawn(DevBridgeMod.Instance, name, spec) ?? throw new InvalidOperationException("Not spawned; the reason is in the log (oben logs).");
        return Describe(npc);
    }

    [BridgeCommand("npcs.despawn", "Removes a DevBridge mod NPC and forgets it in the save.", Params = new[] { "name: the NPC's name" })]
    private static object Despawn(JObject p) => new JObject { ["removed"] = ModNpcs.Despawn(DevBridgeMod.Instance, p.Str("name")) };

    [BridgeCommand("npcs.mods", "Every mod NPC in the scene, whichever mod owns it, with what it is doing.")]
    private static object List(JObject p)
    {
        var list = new JArray();
        foreach (var npc in ModNpcs.All())
            list.Add(Describe(npc));
        return new JObject { ["total"] = list.Count, ["npcs"] = list };
    }

    [BridgeCommand("npcs.order", "Gives a DevBridge mod NPC an order.",
        Params = new[] { "name: the NPC's name", "order: walk, wander, patrol, sit, sleep or stop", "x?,y?,z? or pos?: the point, default the player",
            "yaw?: facing after a walk", "run?: run instead of walk", "radius?: for wander, default 4", "points?: for patrol, [[x,y,z],...]" })]
    private static object Order(JObject p)
    {
        var npc = Mine(p);
        var point = p.Position() ?? PlayerRef.Position ?? npc.Position;
        bool run = p.Bool("run", false);
        bool ok;
        switch (p.Str("order").ToLowerInvariant())
        {
            case "walk": ok = npc.WalkTo(point, p.Float("yaw", 0f), run); break;
            case "wander": ok = npc.Wander(point, p.Float("radius", 4f)); break;
            case "patrol": ok = npc.Patrol(Points(p.Array("points")), run); break;
            case "sit": ok = npc.Sit(point); break;
            case "sleep": ok = npc.Sleep(point); break;
            case "stop": ok = npc.StopOrder(); break;
            default: throw new ArgumentException("order is walk, wander, patrol, sit, sleep or stop.");
        }
        var result = new JObject { ["ok"] = ok, ["order"] = npc.Order.ToString(), ["ai"] = npc.CanMove };
        if (!npc.CanMove)
            result["reason"] = "its body has no AI and cannot walk; spawn it from a template with ai=true in npcs.characters";
        return result;
    }

    [BridgeCommand("npcs.look", "Reads or changes a DevBridge mod NPC's look: a look JSON, a blend shape, or a reset. Lists its meshes.",
        Params = new[] { "name: the NPC's name", "look?: a look JSON object", "mesh?,shape?,weight?: set one blend shape", "reset?: back to the template's look", "meshes?: list meshes and shapes, default false" })]
    private static object Look(JObject p)
    {
        var npc = Mine(p);
        var result = new JObject();
        if (p.Bool("reset", false))
            result["reset"] = npc.ResetLook();
        if (p["look"] is JObject look)
            result["applied"] = npc.SetLook(LookData.FromJson(look.ToString()));
        if (p["shape"] != null)
            result["shapeSet"] = npc.SetBlendShape(p.Str("mesh"), p.Str("shape"), p.Float("weight"));

        var current = npc.Look;
        result["sliders"] = npc.HasSliders;
        result["ownLook"] = npc.HasOwnLook;
        result["look"] = current != null ? JObject.Parse(current.ToJson()) : null;
        var shapes = new JArray();
        foreach (var shape in npc.BlendShapes)
            shapes.Add(new JObject { ["mesh"] = shape.Mesh, ["shape"] = shape.Shape, ["weight"] = shape.Weight });
        result["shapes"] = shapes;
        if (p.Bool("meshes", false))
        {
            var meshes = new JArray();
            foreach (var mesh in npc.Meshes())
                meshes.Add(new JObject { ["name"] = mesh.Name, ["visible"] = mesh.Visible, ["materials"] = new JArray(mesh.Materials), ["shapes"] = new JArray(mesh.BlendShapes) });
            result["meshes"] = meshes;
        }
        return result;
    }

    [BridgeCommand("npcs.characters", "The game's character database: ids to use as mod NPC templates and bodies.",
        Params = new[] { "body?: only characters with a body to spawn, default true", "ai?: only bodies with AI (they can walk and take orders), default false",
            "name?: only names containing this text" })]
    private static object CharacterList(JObject p)
    {
        bool bodyOnly = p.Bool("body", true);
        bool aiOnly = p.Bool("ai", false);
        string name = p.Str("name", null);
        var list = new JArray();
        foreach (var c in Characters.All())
        {
            if ((bodyOnly && !c.HasBody) || (aiOnly && !c.HasAi) || (name != null && c.FullName.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0))
                continue;
            list.Add(new JObject
            {
                ["id"] = c.Id, ["name"] = c.FullName, ["job"] = c.Job, ["gender"] = c.Gender, ["age"] = c.Age,
                ["body"] = c.HasBody, ["ai"] = c.HasAi, ["sliders"] = c.HasSliders, ["player"] = c.IsPlayer, ["mod"] = c.IsModNpc,
            });
        }
        return new JObject { ["total"] = list.Count, ["characters"] = list };
    }

    private static ModNpc Mine(JObject p) =>
        ModNpcs.Get(DevBridgeMod.Instance, p.Str("name")) ?? throw new ArgumentException("DevBridge has no mod NPC of that name here.");

    private static (Vector3 position, float yaw) Spot(JObject p)
    {
        var given = p.Position();
        var body = PlayerRef.Body;
        if (given.HasValue)
            return (given.Value, 0f);
        if (body == null)
            throw new InvalidOperationException("Give a position: there is no player.");
        var forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        // Face the player: the opposite of the player's own facing.
        return (body.position + forward * 2f, Quaternion.LookRotation(-forward).eulerAngles.y);
    }

    private static List<Vector3> Points(JArray array)
    {
        var points = new List<Vector3>();
        foreach (var token in array)
        {
            if (token is JArray a && a.Count >= 3)
                points.Add(new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>()));
        }
        return points;
    }

    internal static JObject Describe(ModNpc npc) => new JObject
    {
        ["owner"] = npc.Owner,
        ["name"] = npc.Name,
        ["id"] = npc.CharacterId,
        ["fullName"] = npc.FullName,
        ["template"] = npc.Template,
        ["body"] = npc.Body,
        ["scene"] = npc.Scene,
        ["pos"] = Vec(npc.Position),
        ["alive"] = npc.Alive,
        ["saved"] = npc.Saved,
        ["order"] = npc.Order.ToString(),
        ["sliders"] = npc.HasSliders,
        ["ai"] = npc.CanMove,
        ["claimedBy"] = NpcControl.ClaimedBy(npc.CharacterId),
    };

    internal static JArray Vec(Vector3 v) => new JArray(Round(v.x), Round(v.y), Round(v.z));

    internal static float Round(float v) => Mathf.Round(v * 100f) / 100f;
}
