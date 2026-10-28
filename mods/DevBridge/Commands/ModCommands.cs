using System;
using System.Collections;
using System.Linq;
using DevBridge.Server;
using ObenLoader.Mods;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Mod management: the same controls as the mod menu, for scripts.</summary>
internal static class ModCommands
{
    [BridgeCommand("mods.list", "Every mod with its state.")]
    private static object List(JObject p) =>
        new JArray(ModRegistry.Mods.Select(m => new JObject
        {
            ["id"] = m.Id,
            ["name"] = m.Manifest.DisplayName,
            ["version"] = m.Manifest.Version,
            ["state"] = m.State.ToString(),
            ["enabled"] = ModRegistry.IsEnabled(m.Id),
            ["hotReload"] = m.CanHotReload,
            ["loadCount"] = m.LoadCount,
            ["error"] = m.Error,
            ["warning"] = m.Warning,
        }).ToArray());

    [BridgeCommand("mods.reload", "Hot reload a mod (or every hot-reload mod when id is omitted).", Params = new[] { "id?" })]
    private static object Reload(JObject p)
    {
        string id = p.Str("id", null);

        // Reloading the bridge (directly or because it builds on the target) closes this very
        // connection, so answer first and reload a moment later.
        if (id == null || ReloadsBridge(id))
        {
            DevBridgeMod.Instance.StartCoroutine(ReloadSoon(id));
            return "reloading";
        }

        var result = new Deferred();
        ModRegistry.Reload(id, error => Finish(result, error, "reloaded"));
        return result;
    }

    private static bool ReloadsBridge(string id)
    {
        var bridge = DevBridgeMod.Instance.Info;
        if (string.Equals(id, bridge.Id, System.StringComparison.OrdinalIgnoreCase))
            return true;

        var pending = new System.Collections.Generic.Stack<string>(bridge.Manifest.Dependencies.Keys);
        while (pending.Count > 0)
        {
            string dep = pending.Pop();
            if (string.Equals(dep, id, System.StringComparison.OrdinalIgnoreCase))
                return true;
            var info = ModRegistry.Find(dep);
            if (info != null)
                foreach (string next in info.Manifest.Dependencies.Keys)
                    pending.Push(next);
        }
        return false;
    }

    private static IEnumerator ReloadSoon(string id)
    {
        yield return new WaitForSecondsRealtime(0.25f);
        if (id == null)
            ModRegistry.ReloadAll();
        else
            ModRegistry.Reload(id);
    }

    [BridgeCommand("mods.load", "Load a mod that isn't running.", Params = new[] { "id" })]
    private static object Load(JObject p)
    {
        var result = new Deferred();
        ModRegistry.Load(p.Str("id"), error => Finish(result, error, "loaded"));
        return result;
    }

    [BridgeCommand("mods.unload", "Unload a running mod (and anything that depends on it).", Params = new[] { "id" })]
    private static object Unload(JObject p)
    {
        var result = new Deferred();
        ModRegistry.Unload(p.Str("id"), error => Finish(result, error, "unloaded"));
        return result;
    }

    [BridgeCommand("mods.rescan", "Look for newly added mod folders.")]
    private static object Rescan(JObject p) => new JArray(ModRegistry.Rescan().Select(m => m.Id).ToArray());

    [BridgeCommand("mods.enable", "Switch a mod on or off for the next launch.", Params = new[] { "id", "enabled: bool" })]
    private static object Enable(JObject p)
    {
        string error = ModRegistry.SetEnabled(p.Str("id"), p.Bool("enabled"));
        if (error != null)
            throw new ArgumentException(error);
        return new JObject { ["restartNeeded"] = ModRegistry.RestartNeeded };
    }

    private static void Finish(Deferred result, string error, string message)
    {
        if (error == null)
            result.Complete(message);
        else
            result.Fail(error);
    }
}
