using System;
using System.Linq;
using DevBridge;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenKit.Values;
using ObenLoader;
using ObenLoader.Mods;
using UnityEngine;

namespace ScriptRunner;

/// <summary>
/// Adds script.run, script.reset and script.list to the dev bridge. The oben tool compiles a C#
/// file or snippet against the game and sends the DLL here; it runs on the main thread.
/// </summary>
public sealed class ScriptRunnerMod : ObenMod
{
    private ScriptHost _scripts;

    internal static ScriptRunnerMod Instance { get; private set; }

    /// <summary>Runs scripts' coroutines.</summary>
    internal MonoBehaviour Host { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Host = CreateObject("Host").AddComponent<CoroutineHost>();
        _scripts = Track(new ScriptHost());

        var router = ModRegistry.Get<DevBridgeMod>().Commands;
        router.Add(this, "script.run", "Run a compiled script (what 'oben run' and 'oben eval' send).",
            new[] { "name: scripts with the same name replace each other", "image: base64 DLL", "symbols?: base64 PDB", "depth?: result depth, default 2" },
            RunScript);
        router.Add(this, "script.reset", "Undo what a script set up (patches, objects, coroutines).",
            new[] { "name?: omit to reset every script" }, ResetScript);
        router.Add(this, "script.list", "Scripts that still have something set up.", new string[0],
            _ => new JArray(_scripts.Active.ToArray()));
    }

    public override void OnUnload() => Instance = null;

    private object RunScript(JObject p)
    {
        byte[] image = Convert.FromBase64String(p.Str("image"));
        byte[] symbols = p["symbols"] != null ? Convert.FromBase64String(p.Str("symbols")) : null;
        var outcome = _scripts.Run(p.Str("name", "eval"), image, symbols);
        return new JObject
        {
            ["result"] = JsonValues.ToJson(outcome.Result, p.Int("depth", 2)),
            ["log"] = new JArray(outcome.Log.ToArray()),
        };
    }

    private object ResetScript(JObject p)
    {
        if (p["name"] != null)
            return _scripts.Reset(p.Str("name")) ? "reset" : "nothing to reset";
        int count = _scripts.Active.Count();
        foreach (string name in _scripts.Active.ToList())
            _scripts.Reset(name);
        return $"reset {count} script(s)";
    }

    private sealed class CoroutineHost : MonoBehaviour { }
}
