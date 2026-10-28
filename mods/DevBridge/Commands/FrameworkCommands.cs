using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Modules;
using ObenFramework.Patching;

namespace DevBridge.Commands;

/// <summary>How the framework's parts and every mod's patch areas came up.</summary>
internal static class FrameworkCommands
{
    [BridgeCommand("framework.modules", "Each framework part: started, its patch area, available, and any error.", Params = new[] { "failed?: only the ones not fully working" })]
    private static object Modules(JObject p)
    {
        bool onlyFailed = p.Bool("failed", false);
        var list = new JArray();
        foreach (var module in FrameworkModules.All)
        {
            if (onlyFailed && module.IsAvailable)
                continue;
            list.Add(new JObject
            {
                ["name"] = module.Name,
                ["available"] = module.IsAvailable,
                ["started"] = module.Started,
                ["patchArea"] = module.PatchArea,
                ["patchesOk"] = module.PatchesOk,
                ["startMs"] = System.Math.Round(module.StartMs, 2),
                ["error"] = module.Error,
                ["updateError"] = module.UpdateError,
            });
        }
        return new JObject
        {
            ["count"] = FrameworkModules.All.Count,
            ["notWorking"] = FrameworkModules.All.Count(m => !m.IsAvailable),
            ["modules"] = list,
        };
    }

    [BridgeCommand("patches.areas", "Patch areas of every mod (or one), with how many patches each has and why one was switched off.",
        Params = new[] { "mod?: a mod id", "failed?: only the switched-off ones" })]
    private static object Areas(JObject p)
    {
        string mod = p.Str("mod", null);
        bool onlyFailed = p.Bool("failed", false);
        var areas = mod == null ? PatchAreas.All() : PatchAreas.Of(mod);
        return new JArray(areas
            .Where(a => !onlyFailed || !a.IsAvailable)
            .Select(a => new JObject
            {
                ["mod"] = a.Owner,
                ["area"] = a.Name,
                ["patches"] = a.Patches,
                ["available"] = a.IsAvailable,
                ["error"] = a.Error,
            }).ToArray());
    }
}
