using System.Linq;
using DevBridge.Server;
using ObenLoader;
using ObenLoader.Mods;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevBridge.Commands;

internal static class CoreCommands
{
    [BridgeCommand("ping", "Check the bridge is up.")]
    private static object Ping(JObject p) => "pong";

    [BridgeCommand("help", "List every bridge method.")]
    private static object Help(JObject p) =>
        new JArray(DevBridgeMod.Instance.Commands.All.Select(c => new JObject
        {
            ["method"] = c.Name,
            ["description"] = c.Description,
            ["params"] = new JArray(c.Params.Cast<object>().ToArray()),
        }));

    [BridgeCommand("game.info", "Scene, versions, mods, fps and time.")]
    private static object Info(JObject p) => new JObject
    {
        ["scene"] = SceneManager.GetActiveScene().name,
        ["inWorld"] = GameController.instance != null,
        ["paused"] = Time.timeScale == 0f,
        ["loaderVersion"] = Loader.Version,
        ["gameVersion"] = Application.version,
        ["gameBuildId"] = Loader.GameBuildId,
        ["unityVersion"] = Application.unityVersion,
        ["fps"] = Mathf.Round(1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f)),
        ["timeScale"] = Time.timeScale,
        ["time"] = Mathf.Round(Time.time),
        ["mods"] = new JArray(ModRegistry.Mods.Select(m => $"{m.Id} {m.Manifest.Version} ({m.State})").ToArray()),
    };

    [BridgeCommand("game.quit", "Close the game.")]
    private static object Quit(JObject p)
    {
        Application.Quit();
        return "bye";
    }

    [BridgeCommand("game.timescale", "Set Time.timeScale.", Params = new[] { "value: 0 pauses, 1 is normal" })]
    private static object TimeScale(JObject p)
    {
        Time.timeScale = Mathf.Max(0f, p.Float("value"));
        return Time.timeScale;
    }

    [BridgeCommand("scenes.loaded", "Every loaded scene, the active one first.")]
    private static object Scenes(JObject p)
    {
        var active = SceneManager.GetActiveScene();
        var list = new JArray(active.name);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene != active)
                list.Add(scene.name + (scene.isLoaded ? "" : " (loading)"));
        }
        return list;
    }
}
