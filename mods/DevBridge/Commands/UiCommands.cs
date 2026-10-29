using System;
using System.Reflection;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Ui;
using ObenLoader.Runtime;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>
/// The framework's UiState and InputCapture, and the kit's windows, prompts, guides and main menu buttons.
/// The bridge doesn't reference ObenUIKit, so the kit is reached through its ObenUIKit.Dev classes by name,
/// in whichever kit copy was loaded last.
/// </summary>
internal static class UiCommands
{
    [BridgeCommand("ui.state", "What the player's screen is busy with (UiState), input captures, and the open kit windows.")]
    private static object State(JObject p)
    {
        var state = JObject.FromObject(UiState.Read());
        state["captures"] = InputCapture.Any;
        state["allCaptured"] = InputCapture.AllCaptured;
        state["jumpCaptured"] = InputCapture.IsCaptured("Jump");
        state["interactCaptured"] = InputCapture.IsCaptured("Interact");
        state["captureHooks"] = InputCapture.IsAvailable;
        state["rawKeyHooks"] = InputCapture.RawKeysAvailable;
        state["pauseHooks"] = UiState.PauseEventsAvailable;
        state["windows"] = Kit("KitDev", "Windows") is object windows ? JToken.FromObject(windows) : new JArray();
        return state;
    }

    [BridgeCommand("ui.demo", "Opens the kit's demo window with every control (tabs, lists, swatches, model preview, windows, HUD).",
        Params = new[] { "close?: true closes it instead" })]
    private static object Demo(JObject p)
    {
        if (p.Bool("close", false))
            return new JObject { ["closed"] = (bool)Kit("KitDev", "CloseWindow", "obenuikit.demo") };
        Kit("KitDemo", "Open");
        return new JObject { ["opened"] = true };
    }

    [BridgeCommand("ui.close", "Closes a kit window by id (see ui.state).", Params = new[] { "id: window id" })]
    private static object Close(JObject p) => new JObject { ["closed"] = (bool)Kit("KitDev", "CloseWindow", p.Str("id")) };

    [BridgeCommand("hud.prompt", "Shows a key prompt (in the game's tutorial prompt when it's free) or lists them. Pressing the key shows a toast.",
        Params = new[] { "text?: prompt text; without it the prompts are only listed", "action?: game action (default Interact)",
            "key?: a KeyCode name instead of an action", "seconds?: how long (default 10)", "hide?: true hides the bridge's prompts" })]
    private static object Prompt(JObject p)
    {
        if (p.Bool("hide", false))
            return new JObject { ["hidden"] = (int)Kit("KitDev", "HideDemoPrompts") };
        string text = p.Str("text", null);
        if (text != null)
        {
            var key = KeyCode.None;
            string keyName = p.Str("key", null);
            if (keyName != null && !Enum.TryParse(keyName, true, out key))
                throw new ArgumentException($"'{keyName}' isn't a KeyCode name.");
            Kit("KitDemoHud", "Prompt", text, p.Str("action", "Interact"), key, p.Float("seconds", 10f));
        }
        return JToken.FromObject(Kit("KitDev", "Prompts"));
    }

    [BridgeCommand("hud.guide", "Starts the kit's demo guide (jump, open the map, walk) or stops it.",
        Params = new[] { "stop?: true stops it" })]
    private static object Guide(JObject p)
    {
        if (p.Bool("stop", false))
            Kit("KitDemoHud", "StopGuide");
        else
            Kit("KitDemoHud", "Guide");
        return JToken.FromObject(Kit("KitDev", "Prompts"));
    }

    [BridgeCommand("menu.buttons", "Mod buttons on the main menu, with where their copies sit; add or remove demo buttons.",
        Params = new[] { "add?: label of a demo button to add", "remove?: true removes the demo buttons" })]
    private static object MenuButtons(JObject p)
    {
        var result = new JObject();
        string add = p.Str("add", null);
        if (add != null)
            result["added"] = (string)Kit("KitDev", "AddMenuButton", add);
        if (p.Bool("remove", false))
            result["removed"] = (int)Kit("KitDev", "RemoveMenuButtons");
        result["buttons"] = JToken.FromObject(Kit("KitDev", "MenuButtons"));
        return result;
    }

    [BridgeCommand("input.capture", "Holds a game action (or a key, or everything) back from the game for a few seconds, through InputCapture.",
        Params = new[] { "action?: game action name, e.g. Jump", "key?: KeyCode name", "all?: true captures every action", "seconds?: default 10" })]
    private static object Capture(JObject p)
    {
        var owner = DevBridgeMod.Instance;
        IDisposable hold;
        string what;
        if (p.Bool("all", false))
        {
            hold = InputCapture.All(owner);
            what = "every action";
        }
        else if (p.Str("key", null) is string keyName)
        {
            if (!Enum.TryParse(keyName, true, out KeyCode key))
                throw new ArgumentException($"'{keyName}' isn't a KeyCode name.");
            hold = InputCapture.Key(owner, key);
            what = "key " + key;
        }
        else
        {
            string action = p.Str("action");
            hold = InputCapture.Action(owner, action);
            what = action;
        }
        float seconds = Mathf.Clamp(p.Float("seconds", 10f), 0.5f, 300f);
        owner.StartCoroutine(ReleaseLater(hold, seconds));
        return new JObject { ["captured"] = what, ["seconds"] = seconds };
    }

    private static System.Collections.IEnumerator ReleaseLater(IDisposable hold, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        hold.Dispose();
    }

    private static object Kit(string type, string method, params object[] args)
    {
        Type found = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.GetName().Name.StartsWith("ObenUIKit", StringComparison.Ordinal))
                continue;
            var candidate = assembly.GetType("ObenUIKit.Dev." + type);
            if (candidate != null)
                found = candidate;
        }
        if (found == null)
            throw new InvalidOperationException("ObenUIKit isn't loaded.");
        var call = found.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(found.FullName, method);
        return call.Invoke(null, args);
    }
}
