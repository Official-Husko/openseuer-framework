using System;
using System.Linq;
using DevBridge.Server;
using ObenFramework.Features;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

internal static class FeatureCommands
{
    [BridgeCommand("features.list", "Every switchable feature of every loaded mod.")]
    private static object List(JObject p) =>
        new JArray(ModFeatures.Features.Select(f => new JObject
        {
            ["mod"] = f.Owner.Info.Id,
            ["id"] = f.Id,
            ["name"] = f.Name,
            ["description"] = f.Description,
            ["enabled"] = f.Enabled,
        }).ToArray());

    [BridgeCommand("features.set", "Switch a feature on or off right now.", Params = new[] { "mod", "id", "enabled: bool" })]
    private static object Set(JObject p)
    {
        string mod = p.Str("mod"), id = p.Str("id");
        var feature = ModFeatures.Features.FirstOrDefault(f =>
            string.Equals(f.Owner.Info.Id, mod, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No feature {mod}/{id}.");
        ModFeatures.SetEnabled(feature, p.Bool("enabled"));
        return feature.Enabled;
    }
}
