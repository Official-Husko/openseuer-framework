using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Scenes;
using ObenFramework.SceneLook;

namespace DevBridge.Commands;

/// <summary>Character looks from the game's scene files through ObenFramework.SceneLook. Looks applied here belong to DevBridge.</summary>
internal static class SceneLookCommands
{
    [BridgeCommand("scenelook.list", "The character looks a scene's file contains (the scene doesn't have to be loaded).",
        Params = new[] { "scene?: default the active scene", "all?: every body, not only characters", "match?: only names, paths or characters containing this" })]
    private static object List(JObject p)
    {
        string scene = p.Str("scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        string match = p.Str("match", null);
        var list = new JArray();
        foreach (var look in SceneLooks.List(scene, !p.Bool("all", false)))
        {
            if (match == null || Contains(look.Name, match) || Contains(look.Path, match) || Contains(look.CharacterName, match))
                list.Add(Describe(look));
        }
        return new JObject { ["scene"] = scene, ["unavailable"] = SceneLooks.Unavailable, ["looks"] = list, ["status"] = SceneLooks.Status() };
    }

    [BridgeCommand("scenelook.find", "Find a look by character id (any scene) or by name in a scene.",
        Params = new[] { "character?: the game's character id", "scene?: with name", "name?: body or character name" })]
    private static object Find(JObject p)
    {
        SceneLookInfo look;
        if (p["character"] != null)
            look = p["scene"] != null ? SceneLooks.FindByCharacter(p.Str("scene"), p.Int("character", 0)) : SceneLooks.FindByCharacter(p.Int("character", 0));
        else
            look = SceneLooks.FindByName(p.Str("scene"), p.Str("name"));
        return look != null ? Describe(look) : null;
    }

    [BridgeCommand("scenelook.apply", "Load a look and put it on a body: an NPC, a prop, any object with an animator.",
        Params = new[]
        {
            "scene: the look's scene", "root: the look's path (or name) from scenelook.list",
            "target: the body, by #id, address, GUID or path", "mode?: apply (default), skeleton or reskin", "describe?: also return the look report",
        })]
    private static object Apply(JObject p)
    {
        var body = SceneQuery.Resolve(p.Str("target"));
        var look = SceneLooks.Load(DevBridgeMod.Instance, p.Str("scene"), p.Str("root"));
        if (!look.Ready)
            throw new InvalidOperationException("The look didn't load: " + look.Reason);
        string mode = p.Str("mode", "apply");
        string reason;
        bool applied = mode == "skeleton" ? SceneLooks.ApplyToSkeleton(DevBridgeMod.Instance, look, body, out reason)
            : mode == "reskin" ? SceneLooks.Reskin(DevBridgeMod.Instance, look, body, out reason)
            : SceneLooks.Apply(DevBridgeMod.Instance, look, body, out reason);
        var rig = applied ? SceneLooks.RigOf(DevBridgeMod.Instance, body, out _) : null;
        return new JObject
        {
            ["applied"] = applied,
            ["reason"] = reason,
            ["path"] = rig?.Path,
            ["meshes"] = look.MeshCount,
            ["vertices"] = look.VertexCount,
            ["report"] = p.Bool("describe", false) ? SceneLooks.Describe(look, body) : null,
        };
    }

    [BridgeCommand("scenelook.remove", "Take a look DevBridge applied off a body.", Params = new[] { "target: the body" })]
    private static object Remove(JObject p)
    {
        bool removed = SceneLooks.Remove(DevBridgeMod.Instance, SceneQuery.Resolve(p.Str("target")), out string reason);
        return new JObject { ["removed"] = removed, ["reason"] = reason };
    }

    [BridgeCommand("scenelook.applied", "Every body wearing a scene look, which mod put it there and how.")]
    private static object Applied(JObject p)
    {
        var list = new JArray();
        foreach (var applied in SceneLooks.Applied())
        {
            list.Add(new JObject
            {
                ["body"] = applied.Body != null ? "#" + applied.Body.GetInstanceID() : null,
                ["name"] = applied.Body != null ? applied.Body.name : null,
                ["mod"] = applied.ModId,
                ["look"] = applied.Look,
                ["path"] = applied.Path,
            });
        }
        return new JObject { ["applied"] = list, ["status"] = SceneLooks.Status() };
    }

    private static bool Contains(string text, string match) =>
        text != null && text.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0;

    private static JObject Describe(SceneLookInfo look) => new JObject
    {
        ["name"] = look.Name,
        ["path"] = look.Path,
        ["level"] = look.Level,
        ["renderers"] = look.Renderers,
        ["bones"] = look.Bones,
        ["rig"] = look.RigKind,
        ["avatar"] = look.AvatarKind,
        ["npc"] = look.IsNpc,
        ["clean"] = look.Clean,
        ["character"] = look.HasCharacter ? look.CharacterId + " " + look.CharacterName : null,
        ["problems"] = look.Clean ? null : new JArray(look.Problems),
    };
}
