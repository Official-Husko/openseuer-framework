using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Assets;

namespace DevBridge.Commands;

/// <summary>What mods have loaded through the framework's asset loaders.</summary>
internal static class AssetCommands
{
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
