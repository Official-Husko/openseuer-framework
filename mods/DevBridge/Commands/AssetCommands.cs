using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Assets;

namespace DevBridge.Commands;

/// <summary>What mods have loaded through the framework's asset loaders, and volume channels lowered by the bridge.</summary>
internal static class AssetCommands
{
    private static readonly Dictionary<VolumeChannel, VolumeLowering> Lowered = new Dictionary<VolumeChannel, VolumeLowering>();

    [BridgeCommand("sounds.volume", "The game's volume channels: levels and mod shares, or lowers one (owned by the bridge) or gives it back.",
        Params = new[] { "channel?: Master, Sound, Music or Nasty", "share?: 0 to 1 of the player's volume", "clear?: true gives the channel back" })]
    private static object Volume(JObject p)
    {
        string name = p.Str("channel", null);
        if (name != null)
        {
            if (!Enum.TryParse(name, true, out VolumeChannel channel))
                throw new ArgumentException($"No volume channel '{name}'.");
            if (p.Bool("clear", false) && Lowered.TryGetValue(channel, out var old))
            {
                old.Dispose();
                Lowered.Remove(channel);
            }
            else if (p["share"] != null)
            {
                if (Lowered.TryGetValue(channel, out var lowering))
                    lowering.Share = p.Float("share", 1f);
                else
                    Lowered[channel] = SoundVolumes.Lower(DevBridgeMod.Instance, channel, p.Float("share", 1f));
            }
        }
        var channels = new JArray();
        foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
            channels.Add(new JObject { ["channel"] = channel.ToString(), ["level"] = SoundVolumes.Level(channel), ["share"] = SoundVolumes.Share(channel) });
        return new JObject { ["available"] = SoundVolumes.IsAvailable, ["channels"] = channels };
    }

    [BridgeCommand("assets.list", "Textures, sprites, sounds and models each mod has loaded, with sizes and memory use.",
        Params = new[] { "mod?: only this mod id", "kind?: texture, sprite, sound or model" })]
    private static object List(JObject p)
    {
        string mod = p.Str("mod", null);
        string kind = p.Str("kind", null);

        var mods = new Dictionary<string, JObject>(System.StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var asset in ModAssets.List(mod))
        {
            if (kind != null && !string.Equals(asset.Kind, kind, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (!mods.TryGetValue(asset.ModId, out var entry))
            {
                entry = new JObject { ["mod"] = asset.ModId, ["bytes"] = 0L, ["assets"] = new JArray() };
                mods[asset.ModId] = entry;
            }
            ((JArray)entry["assets"]).Add(new JObject
            {
                ["kind"] = asset.Kind,
                ["name"] = asset.Name,
                ["source"] = asset.Source,
                ["detail"] = asset.Detail,
                ["bytes"] = asset.Bytes,
            });
            entry["bytes"] = entry.Value<long>("bytes") + asset.Bytes;
            total += asset.Bytes;
        }

        return new JObject
        {
            ["totalBytes"] = total,
            ["mods"] = new JArray(mods.Values),
        };
    }
}
