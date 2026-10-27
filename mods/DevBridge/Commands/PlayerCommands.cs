using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Reading and changing the player. Every command fails with a clear message at the main menu.</summary>
internal static class PlayerCommands
{
    [BridgeCommand("player.get", "Player position, facing, scene, state flags and every stat.")]
    private static object Get(JObject p)
    {
        var body = PlayerRef.Body ?? throw NoPlayer();
        var stats = new JObject();
        foreach (var stat in PlayerStat.All)
        {
            if (stat.TryGet(out float value))
                stats[stat.Name] = Round(value);
        }

        return new JObject
        {
            ["scene"] = PlayerRef.SceneName,
            ["position"] = Vec(body.position),
            ["yaw"] = Round(PlayerRef.Yaw ?? 0f),
            ["pitch"] = Round(PlayerRef.Pitch ?? 0f),
            ["velocity"] = Vec(PlayerRef.Velocity),
            ["godmode"] = PlayerState.Godmode,
            ["dead"] = PlayerState.IsDead,
            ["seated"] = PlayerState.IsSeated,
            ["sleeping"] = PlayerState.IsSleeping,
            ["crouching"] = PlayerState.IsCrouching,
            ["swimming"] = PlayerState.IsSwimming,
            ["firstPerson"] = PlayerState.IsFirstPerson,
            ["inputLocked"] = PlayerState.InputLocked,
            ["stats"] = stats,
        };
    }

    [BridgeCommand("player.teleport", "Move the player. Stands up from a chair first and keeps the player's scale.",
        Params = new[] { "x,y,z or pos: [x,y,z]", "yaw?: degrees to face", "pitch?: camera degrees, positive down", "ground?: snap down to the floor under it" })]
    private static object Teleport(JObject p)
    {
        var target = p.Position() ?? throw new ArgumentException("Give x, y and z, or pos: [x, y, z].");
        if (p.Bool("ground", false))
        {
            if (!PlayerTeleport.TryFindGround(target, out var ground))
                throw new InvalidOperationException($"Nothing to stand on below {Vec(target)}.");
            target = ground;
        }

        PlayerTeleport.To(target, p["yaw"]?.Value<float>(), p["pitch"]?.Value<float>());
        return new JObject { ["position"] = Vec(PlayerRef.Position ?? target), ["yaw"] = Round(PlayerRef.Yaw ?? 0f) };
    }

    [BridgeCommand("player.stat", "Read or change one stat by its player_stats console name (see player.stats for the list).",
        Params = new[] { "name: e.g. hunger, thirst, alcohol_in_blood", "value?: new value; omit to just read", "mode?: set (default) or add" })]
    private static object Stat(JObject p)
    {
        var stat = PlayerStat.Require(p.Str("name"));
        if (p["value"] == null)
            return Describe(stat);

        float value = p.Float("value");
        string mode = p.Str("mode", "set").ToLowerInvariant();
        float stored = mode switch
        {
            "set" => stat.Set(value),
            "add" => stat.Add(value),
            _ => throw new ArgumentException($"Unknown mode '{mode}'. Use set or add."),
        };
        return new JObject { ["name"] = stat.Name, ["value"] = Round(stored) };
    }

    [BridgeCommand("player.stats", "List every stat with its group, range, description and current value.")]
    private static object Stats(JObject p) => new JArray(PlayerStat.All.Select(Describe).ToArray());

    [BridgeCommand("player.heal", "Restore health. With no amount: full health, no bleeding.",
        Params = new[] { "amount?: health to restore", "needs?: also clear hunger, thirst, tiredness and the rest" })]
    private static object Heal(JObject p)
    {
        if (!PlayerRef.Exists)
            throw NoPlayer();

        if (p["amount"] != null)
            PlayerVitals.Heal(p.Float("amount"));
        else
            PlayerVitals.FullHeal();
        if (p.Bool("needs", false))
            PlayerVitals.ClearNeeds();
        return new JObject { ["health"] = Round(PlayerVitals.Health ?? 0f) };
    }

    [BridgeCommand("player.godmode", "Turn the game's god mode on or off, or read it.", Params = new[] { "enabled?: bool" })]
    private static object Godmode(JObject p)
    {
        if (!PlayerRef.Exists)
            throw NoPlayer();
        if (p["enabled"] != null)
            PlayerState.Godmode = p.Bool("enabled");
        return PlayerState.Godmode;
    }

    private static JObject Describe(PlayerStatRef stat)
    {
        var info = new JObject { ["name"] = stat.Name, ["group"] = stat.Group, ["description"] = stat.Description };
        if (stat.Min.HasValue)
            info["min"] = stat.Min.Value;
        if (stat.Max.HasValue)
            info["max"] = stat.Max.Value;
        if (stat.TryGet(out float value))
            info["value"] = Round(value);
        return info;
    }

    private static JArray Vec(Vector3 v) => new JArray(Round(v.x), Round(v.y), Round(v.z));

    private static float Round(float value) => Mathf.Round(value * 1000f) / 1000f;

    private static InvalidOperationException NoPlayer() =>
        new InvalidOperationException("There is no player right now (main menu or loading).");
}
