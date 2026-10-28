using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using ObenFramework.World.Props;
using ObenLoader;
using ObenLoader.Mods;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>World props through ObenFramework.World.Props. Props placed here belong to DevBridge unless another mod is named.</summary>
internal static class PropCommands
{
    [BridgeCommand("props.list", "Recorded world props of every mod (installed or not), with whether each is in the world.",
        Params = new[] { "scene?: only this scene", "mod?: only this mod" })]
    private static object List(JObject p)
    {
        var list = new JArray();
        foreach (var info in WorldProps.All(p.Str("scene", null), p.Str("mod", null)))
            list.Add(Describe(info));
        return new JObject { ["total"] = list.Count, ["live"] = WorldProps.LiveCount, ["props"] = list };
    }

    [BridgeCommand("props.spawn", "Place a prop (or replace one with the same id). Defaults to 2 m in front of the player.",
        Params = new[]
        {
            "id: prop id", "kind?: cube (default), sphere, cylinder, plane, model, furniture, item, light, spot, anchor",
            "source?: model name (modId/key), furniture id or item id, for those kinds", "pos?: [x,y,z] or x/y/z", "yaw?: degrees",
            "scale?: uniform scale, default 1", "scene?: default the active scene", "collider?: default true", "static?: default false",
            "interact?: interact key (needs a handler from the owning mod)", "title?: prompt title", "data?: free text",
            "color?: [r,g,b] for lights", "range?: light range, default 5", "intensity?: light intensity, default 1",
            "mod?: owning mod id, default DevBridge",
        })]
    private static object Spawn(JObject p)
    {
        var owner = Owner(p);
        var position = p.Position() ?? InFront();
        var rotation = Quaternion.Euler(0f, p.Float("yaw", PlayerRef.Yaw ?? 0f), 0f);
        float scale = p.Float("scale", 1f);
        var options = new WorldPropOptions
        {
            Collider = p.Bool("collider", true),
            IsStatic = p.Bool("static", false),
            InteractKey = p.Str("interact", null),
            Title = p.Str("title", null),
            Data = p.Str("data", null),
        };
        string scene = p.Str("scene", WorldStateScene());
        var go = WorldProps.Place(owner, p.Str("id"), scene, position, rotation, Vector3.one * scale, Source(p), options);
        if (go == null && WorldProps.LastProblem != null)
            throw new InvalidOperationException(WorldProps.LastProblem);
        return new JObject
        {
            ["placed"] = true,
            ["live"] = go != null,
            ["object"] = go != null ? "#" + go.GetInstanceID() : null,
            ["position"] = new JArray(position.x, position.y, position.z),
        };
    }

    [BridgeCommand("props.move", "Move a prop of a mod (static props can't move).",
        Params = new[] { "id", "pos: [x,y,z] or x/y/z", "yaw?: degrees, default 0", "mod?: default DevBridge" })]
    private static object Move(JObject p)
    {
        var position = p.Position() ?? throw new ArgumentException("Give 'pos' or x/y/z.");
        bool moved = WorldProps.Move(Owner(p), p.Str("id"), position, Quaternion.Euler(0f, p.Float("yaw", 0f), 0f));
        return new JObject { ["moved"] = moved, ["problem"] = moved ? null : WorldProps.LastProblem };
    }

    [BridgeCommand("props.remove", "Remove a prop of a mod from the world and the save, or all of them with all=true.",
        Params = new[] { "id?: prop id", "all?: remove every prop of the mod", "mod?: default DevBridge" })]
    private static object Remove(JObject p)
    {
        var owner = Owner(p);
        if (p.Bool("all", false))
            return new JObject { ["removed"] = WorldProps.Clear(owner) };
        bool removed = WorldProps.Remove(owner, p.Str("id"));
        return new JObject { ["removed"] = removed ? 1 : 0, ["problem"] = removed ? null : WorldProps.LastProblem };
    }

    private static WorldPropSource Source(JObject p)
    {
        string kind = p.Str("kind", "cube").ToLowerInvariant();
        switch (kind)
        {
            case "cube": return WorldPropSource.Primitive(WorldPrimitive.Cube);
            case "sphere": return WorldPropSource.Primitive(WorldPrimitive.Sphere);
            case "cylinder": return WorldPropSource.Primitive(WorldPrimitive.Cylinder);
            case "plane": return WorldPropSource.Primitive(WorldPrimitive.Plane);
            case "model": return WorldPropSource.Model(p.Str("source"));
            case "furniture": return WorldPropSource.Furniture(p.Str("source"), p.Int("alt", 0));
            case "item": return WorldPropSource.Item(int.Parse(p.Str("source")));
            case "anchor": return WorldPropSource.Anchor();
            case "light":
            case "spot":
                var c = p["color"] as JArray;
                var color = c != null && c.Count >= 3 ? new Color(c[0].Value<float>(), c[1].Value<float>(), c[2].Value<float>()) : Color.white;
                return WorldPropSource.Light(kind == "spot" ? WorldPropLight.Spot : WorldPropLight.Point, color, p.Float("range", 5f), p.Float("intensity", 1f), p.Float("angle", 45f));
            default:
                throw new ArgumentException($"Unknown prop kind '{kind}'.");
        }
    }

    private static ObenMod Owner(JObject p)
    {
        string id = p.Str("mod", null);
        if (id == null)
            return DevBridgeMod.Instance;
        return ModRegistry.Find(id)?.Instance ?? throw new ArgumentException($"Mod '{id}' isn't loaded.");
    }

    private static Vector3 InFront()
    {
        var body = PlayerRef.Body;
        if (body == null)
            throw new InvalidOperationException("No player; give 'pos'.");
        return body.position + body.forward * 2f + Vector3.up * 0.5f;
    }

    private static string WorldStateScene() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    private static JObject Describe(WorldPropInfo info) => new JObject
    {
        ["mod"] = info.ModId,
        ["id"] = info.PropId,
        ["scene"] = info.Scene,
        ["kind"] = info.Kind.ToString(),
        ["source"] = info.Source,
        ["position"] = new JArray(info.Position.x, info.Position.y, info.Position.z),
        ["scale"] = info.Scale.x,
        ["live"] = info.Live,
        ["object"] = info.Live ? "#" + info.Object.GetInstanceID() : null,
        ["interact"] = string.IsNullOrEmpty(info.InteractKey) ? null : info.InteractKey,
        ["data"] = string.IsNullOrEmpty(info.Data) ? null : info.Data,
    };
}
