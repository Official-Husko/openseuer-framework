using System;
using ObenFramework.Game;
using ObenUIKit.Controls;
using UnityEngine;

namespace ObenUIKit.Dev;

/// <summary>The demo's HUD tab and the prompt and guide examples the dev bridge starts.</summary>
public static class KitDemoHud
{
    public const string GuideId = "obenuikit.demo.guide";

    private static IDisposable _crosshair;
    private static IDisposable _hud;

    /// <summary>Shows a demo prompt over a game action or a key, for <paramref name="seconds"/>; pressing it shows a toast.</summary>
    public static HudPrompt Prompt(string text, string action, KeyCode key, float seconds)
    {
        var options = new PromptOptions { Seconds = seconds, OnPress = () => UiKit.Toast("Prompt key pressed"), Once = true };
        var kit = UIKitMod.Instance;
        return key != KeyCode.None ? Hud.Prompt(kit, text, key, options) : Hud.Prompt(kit, text, action ?? "Interact", options);
    }

    /// <summary>Starts a short demo guide through the game's tutorial prompt: jump, open the map, walk, done.</summary>
    public static HudGuide Guide()
    {
        _measuring = false;
        var guide = Hud.Guide(UIKitMod.Instance, GuideId)
            .Step("Jump", "Jump")
            .Step("Open your map", "Map")
            .Step("Walk a few steps", () => PlayerMoved(), 0f)
            .Step("That's the demo guide", null, seconds: 3f);
        guide.StepStarted += i => UIKitMod.Instance.Log.Info("Demo guide: step " + (i + 1));
        guide.Finished += () => UiKit.Toast("Demo guide finished");
        return guide.Start();
    }

    public static void StopGuide()
    {
        var runtime = PromptRuntime.Current;
        if (runtime == null)
            return;
        for (int i = runtime.Guides.Count - 1; i >= 0; i--)
        {
            if (i < runtime.Guides.Count && runtime.Guides[i].Id == GuideId)
                runtime.Guides[i].Stop();
        }
    }

    internal static void Build(UiBuilder ui)
    {
        ui.Label("Prompts and guides only show in a level, with no window open: the buttons close this window first.", muted: true);
        ui.Button("Prompt over Interact (10 s)", () => Later(ui, () => Prompt("Demo prompt", "Interact", KeyCode.None, 10f)));
        ui.Button("Prompt over G (10 s)", () => Later(ui, () => Prompt("Press G for a toast", null, KeyCode.G, 10f)));
        ui.Button("Start the demo guide", () => Later(ui, () => Guide()));
        ui.Button("Stop the demo guide", StopGuide);

        ui.Header("The game's HUD");
        ui.Toggle("Hide the crosshair", _crosshair != null, on => _crosshair = Swap(_crosshair, on, Hud.HideCrosshair));
        ui.Toggle("Hide the whole HUD", _hud != null, on => _hud = Swap(_hud, on, Hud.HideGameHud));
        ui.Button("A hint", () => Hud.Hint(UIKitMod.Instance, "This is a hint. {key:Interact} is your interact key.", 6f, "Demo"));
        ui.Button("A subtitle", () => Hud.Subtitle("Somebody is following you."));
    }

    private static IDisposable Swap(IDisposable lease, bool on, Func<ObenLoader.Runtime.ITracker, IDisposable> take)
    {
        lease?.Dispose();
        return on ? take(UIKitMod.Instance) : null;
    }

    private static void Later(UiBuilder ui, Action action)
    {
        if (!WorldState.InWorld)
        {
            UiKit.Toast("Prompts and guides only show in a level.");
            return;
        }
        ui.Window.Close();
        action();
    }

    private static Vector3 _start;
    private static bool _measuring;

    // Done once the player is three metres from where the step started.
    private static bool PlayerMoved()
    {
        var body = PlayerLocator.instance;
        if (body == null)
            return false;
        if (!_measuring)
        {
            _measuring = true;
            _start = body.transform.position;
            return false;
        }
        bool moved = (body.transform.position - _start).sqrMagnitude > 9f;
        if (moved)
            _measuring = false;
        return moved;
    }
}
