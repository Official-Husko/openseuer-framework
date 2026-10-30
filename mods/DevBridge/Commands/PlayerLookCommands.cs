using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player.Look;
using ObenFramework.Player.Survival;

namespace DevBridge.Commands;

/// <summary>Reading the player's look (and previewing sliders on it) and the survival adjustments mods have set.</summary>
internal static class PlayerLookCommands
{
    [BridgeCommand("player.look", "The local player's look (body sliders and worn items by asset name) as JSON; preview= moves sliders live.",
        Params = new[] { "bytes?: true to also return the compact network form as base64",
            "preview?: {slider: value, ...} shown on the live body at once (PlayerLook.PreviewOnPlayer); a level load puts the real look back",
            "options?: a slot name; lists what the player's own Appearance offers for it" })]
    private static object Look(JObject p)
    {
        var look = PlayerLook.Capture() ?? throw new InvalidOperationException("There is no player right now (main menu or loading).");
        if (p["preview"] is JObject changes)
        {
            var merged = JObject.Parse(look.ToJson());
            merged.Merge(changes);
            if (!PlayerLook.PreviewOnPlayer(LookData.FromJson(merged.ToString())))
                throw new InvalidOperationException("The player's body isn't built yet.");
            look = PlayerLook.Capture();
        }
        var result = new JObject { ["look"] = JObject.Parse(look.ToJson()) };
        if (p["options"] != null)
            result["options"] = new JArray(PlayerLook.Options(p.Str("options"), look.male, PlayerLook.PlayerAppearance));
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
