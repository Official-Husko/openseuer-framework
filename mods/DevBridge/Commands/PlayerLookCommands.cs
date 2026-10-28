using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player.Look;
using ObenFramework.Player.Survival;

namespace DevBridge.Commands;

/// <summary>Reading the player's look and the survival adjustments mods have set. Read-only.</summary>
internal static class PlayerLookCommands
{
    [BridgeCommand("player.look", "The local player's look (body sliders and worn items by asset name) as JSON.",
        Params = new[] { "bytes?: true to also return the compact network form as base64" })]
    private static object Look(JObject p)
    {
        var look = PlayerLook.Capture() ?? throw new InvalidOperationException("There is no player right now (main menu or loading).");
        var result = new JObject { ["look"] = JObject.Parse(look.ToJson()) };
        if (p.Bool("bytes", false))
        {
            byte[] bytes = look.ToBytes();
            result["bytes"] = Convert.ToBase64String(bytes);
            result["byteCount"] = bytes.Length;
        }
        return result;
    }

    [BridgeCommand("player.drain", "Every survival drain adjustment mods have set (who, which need, multiplier, offset) and the combined result per need.")]
    private static object Drain(JObject p)
    {
        var entries = new JArray();
        foreach (var entry in StatDrain.Active)
        {
            entries.Add(new JObject
            {
                ["mod"] = entry.OwnerId,
                ["drain"] = entry.Drain.ToString(),
                ["key"] = entry.Key,
                ["multiplier"] = entry.Multiplier,
                ["offsetPerHour"] = entry.OffsetPerHour,
            });
        }

        var combined = new JObject();
        foreach (PlayerDrain drain in Enum.GetValues(typeof(PlayerDrain)))
        {
            combined[drain.ToString()] = new JObject
            {
                ["multiplier"] = StatDrain.Multiplier(drain),
                ["offsetPerHour"] = StatDrain.Offset(drain),
            };
        }

        return new JObject { ["entries"] = entries, ["combined"] = combined, ["thresholdWatches"] = StatThresholds.Count };
    }
}
