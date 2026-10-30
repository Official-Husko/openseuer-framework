using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Npcs;
using ObenFramework.Player;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Extras, node groups, claims, saved mod NPCs and every NPC object in the scene. Claims and extras belong to DevBridge.</summary>
internal static class NpcSceneCommands
{
    private static Vector3 From => PlayerRef.Position ?? Vector3.zero;

    [BridgeCommand("npcs.extras", "DevBridge's extras (bodies that only stand there). With name and template spawns one in front of the player.",
        Params = new[] { "name?: spawn this extra", "template?: whose look", "body?: whose body", "x?,y?,z? or pos?: where", "yaw?: facing",
            "despawn?: remove the named extra", "clear?: remove them all", "anim?: play a clip of the extra's own Animator by name (with name)" })]
    private static object Extras(JObject p)
    {
        var owner = DevBridgeMod.Instance;
        string name = p.Str("name", null);
        var result = new JObject();
        if (p.Bool("clear", false))
            result["cleared"] = NpcExtras.Clear(owner);
        else if (name != null && p.Bool("despawn", false))
            result["despawned"] = NpcExtras.Despawn(owner, name);
        else if (name != null && p["template"] != null)
        {
            var spot = ModNpcCommands.Spot(p);
            var extra = NpcExtras.Spawn(owner, name, new ModNpcSpec
            {
                Template = p.Int("template", 0), Body = p.Int("body", 0), Position = spot.position, Yaw = p.Float("yaw", spot.yaw),
            }) ?? throw new InvalidOperationException("Not spawned; the reason is in the log (oben logs).");
            result["spawned"] = extra.ToString();
        }
        if (name != null && p["anim"] != null)
        {
            var extra = NpcExtras.Get(owner, name) ?? throw new ArgumentException($"No extra '{name}'.");
            result["playing"] = NpcAnimation.Play(owner, extra.Animator, Clip(extra.Animator, p.Str("anim"))) != null;
        }

        var list = new JArray();
        foreach (var extra in NpcExtras.All())
        {
            var root = extra.Root;
            var scripts = new JArray();
            foreach (var part in root.GetComponentsInChildren<MonoBehaviour>(true))
                scripts.Add(part.GetType().Name);
            list.Add(new JObject
            {
                ["owner"] = extra.Owner, ["name"] = extra.Name, ["template"] = extra.Template, ["body"] = extra.Body,
                ["pos"] = ModNpcCommands.Vec(extra.Position), ["scripts"] = scripts, ["animator"] = extra.Animator != null,
            });
        }
        result["extras"] = list;
        return result;
    }

    [BridgeCommand("npcs.nodes", "The scene's node groups (patrol routes and standing spots), nearest to the player first.",
        Params = new[] { "limit?: how many, default 20" })]
    private static object Nodes(JObject p)
    {
        var list = new JArray();
        int limit = p.Int("limit", 20);
        var groups = NpcNodes.Groups(From);
        foreach (var group in groups)
        {
            if (list.Count >= limit)
                break;
            list.Add(new JObject
            {
                ["id"] = group.Id, ["name"] = group.Name, ["nodes"] = group.Nodes, ["mode"] = group.Mode, ["loop"] = group.Loop,
                ["canStand"] = group.CanStand, ["pos"] = ModNpcCommands.Vec(group.Position), ["distance"] = Math.Round(Vector3.Distance(From, group.Position), 1),
            });
        }
        return new JObject { ["total"] = groups.Count, ["groups"] = list };
    }

    [BridgeCommand("npcs.claim", "Claims an NPC for DevBridge and does something with the claim.",
        Params = new[] { "id: the NPC's character id", "do: patrol, stand, stopnodes, hold, anim, stopanim, pause, resume or release",
            "group?: node group id for patrol and stand (see npcs.nodes)", "holds?: for hold, e.g. Freeze,Talk (None lifts them)",
            "clip?: for anim, a clip of the NPC's own Animator by name", "loop?: for anim, default true" })]
    private static object Claim(JObject p)
    {
        int id = p.Int("id", -1);
        string action = p.Str("do").ToLowerInvariant();
        if (action == "release")
        {
            var held = NpcControl.ClaimedBy(id) == DevBridgeMod.Instance.Info.Id ? NpcControl.Claim(DevBridgeMod.Instance, id) : null;
            held?.Dispose();
            return new JObject { ["released"] = held != null };
        }

        var claim = NpcControl.Claim(DevBridgeMod.Instance, id) ?? throw new InvalidOperationException($"NPC {id} can't be claimed (not here, no AI, dead, or another mod holds it).");
        object ok;
        switch (action)
        {
            case "patrol": ok = claim.Patrol(Group(p)); break;
            case "stand": ok = claim.Stand(Group(p)); break;
            case "stopnodes": claim.StopNodes(); ok = true; break;
            case "hold": ok = claim.Hold((NpcHolds)Enum.Parse(typeof(NpcHolds), p.Str("holds", "None"), true)); break;
            case "anim":
                var animator = claim.Npc.GetComponentInChildren<Animator>();
                ok = claim.PlayAnimation(Clip(animator, p.Str("clip")), p.Bool("loop", true)) != null;
                break;
            case "stopanim": claim.StopAnimation(); ok = true; break;
            case "pause": ok = claim.Pause(); break;
            case "resume": claim.Resume(); ok = true; break;
            default: throw new ArgumentException("Unknown do.");
        }
        var state = claim.Npc != null && claim.Npc.npcController != null ? claim.Npc.npcController.Data?.state : null;
        return new JObject
        {
            ["ok"] = JToken.FromObject(ok), ["onNodes"] = claim.OnNodes, ["paused"] = claim.Paused, ["holds"] = claim.Holds.ToString(),
            ["activity"] = state?.CurrentActivity?.activityName, ["pos"] = claim.Npc != null ? ModNpcCommands.Vec(claim.Npc.transform.position) : null,
        };
    }

    [BridgeCommand("npcs.saved", "Mod NPCs kept in the loaded save, in every scene, also those of mods that aren't installed.")]
    private static object Saved(JObject p)
    {
        var list = new JArray();
        foreach (var npc in ModNpcs.SavedAll())
        {
            list.Add(new JObject
            {
                ["mod"] = npc.Mod, ["name"] = npc.Name, ["fullName"] = (npc.FirstName + " " + npc.LastName).Trim(), ["template"] = npc.Template,
                ["scene"] = npc.Scene, ["pos"] = ModNpcCommands.Vec(npc.Position), ["here"] = npc.Here, ["modLoaded"] = npc.ModLoaded,
            });
        }
        return list;
    }

    [BridgeCommand("npcs.actors", "Every NPC object in the scene, also those without a character (id -1), with a handle for them.",
        Params = new[] { "nocharacter?: only those without a character, default false" })]
    private static object Actors(JObject p)
    {
        bool only = p.Bool("nocharacter", false);
        var list = new JArray();
        int without = 0;
        var actors = Npcs.Actors();
        foreach (var actor in actors)
        {
            if (!actor.HasCharacter)
                without++;
            if (only && actor.HasCharacter)
                continue;
            list.Add(new JObject
            {
                ["id"] = actor.Id, ["name"] = actor.Name, ["fullName"] = actor.FullName, ["handle"] = actor.Handle, ["pos"] = ModNpcCommands.Vec(actor.Position),
                ["active"] = actor.Active, ["dead"] = actor.Dead, ["ai"] = actor.HasController, ["found"] = Npcs.FindActor(actor.Handle) == actor.Live,
            });
        }
        return new JObject { ["total"] = actors.Count, ["withoutCharacter"] = without, ["actors"] = list };
    }

    private static NPC.NodeGroup Group(JObject p) =>
        NpcNodes.Find(p.Str("group")) ?? throw new ArgumentException("No node group with that id (see npcs.nodes).");

    private static AnimationClip Clip(Animator animator, string name)
    {
        var controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null)
            throw new ArgumentException("It has no animations.");
        // Some clips come twice, generic and humanoid; a humanoid body needs the humanoid one.
        var names = new List<string>();
        AnimationClip match = null;
        foreach (var clip in controller.animationClips)
        {
            if (clip == null)
                continue;
            if (string.Equals(clip.name, name, StringComparison.OrdinalIgnoreCase) && (match == null || clip.humanMotion == animator.isHuman))
                match = clip;
            if (!names.Contains(clip.name))
                names.Add(clip.name);
        }
        if (match != null)
            return match;
        throw new ArgumentException($"No clip '{name}'. It has: {string.Join(", ", names)}");
    }
}
