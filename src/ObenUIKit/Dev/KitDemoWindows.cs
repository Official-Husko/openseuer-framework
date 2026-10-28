using System;
using ObenFramework.Ui;
using ObenUIKit.Controls;
using ObenUIKit.Menu;
using ObenUIKit.Windows;
using UnityEngine;

namespace ObenUIKit.Dev;

/// <summary>The demo's Windows tab: docked, floating, display-only and fragile windows, main menu buttons.</summary>
internal static class KitDemoWindows
{
    private static int _menuButtons;

    public static void Build(UiBuilder ui)
    {
        var kit = UIKitMod.Instance;
        ui.Header("Docked and floating");
        ui.Row(r =>
        {
            r.Button("Left", () => Side(WindowDock.Left));
            r.Button("Right", () => Side(WindowDock.Right));
            r.Button("Floating", Floating);
        });
        ui.Tooltip("A floating window is dragged by its title.");
        ui.Button("Display-only panel (Esc can't close it)", Passive);
        ui.Tooltip("LockInput off: the game keeps the keys. Close it from here or with the Close button below.");
        ui.Button("Close the panel", () => UiKit.Find("obenuikit.demo.passive")?.Close());

        ui.Header("Lifetime");
        ui.Button("A window that closes on its first error", () =>
        {
            var window = UiKit.Open(kit, "obenuikit.demo.fragile", "Fragile", w =>
            {
                w.Label("The button throws. With CloseOnError the window closes and the log says why.", muted: true);
                w.Button("Throw", () => throw new InvalidOperationException("Thrown on purpose by the demo"));
            }, new WindowOptions { Dock = WindowDock.Floating, Width = 620f, Height = 420f, CloseOnError = true });
            window.Closed += () => UiKit.Toast("Fragile window closed: " + window.CloseReason);
        });
        ui.Button("Capture Jump while a window is open", () =>
        {
            var window = UiKit.Open(kit, "obenuikit.demo.capture", "Jump is captured", w =>
            {
                w.Label("While this window is open the game's Jump action reads as not pressed.", muted: true);
                w.Live(() => "Captured now: " + InputCapture.IsCaptured("Jump"));
            }, new WindowOptions { Dock = WindowDock.Right, Width = 560f, LockInput = false });
            // The window is the capture's owner: the capture ends when it closes.
            InputCapture.Action(window, "Jump");
        });

        ui.Header("Main menu buttons");
        ui.Label("Added buttons show under the main menu's grid (go back to the main menu to see them).", muted: true);
        ui.Row(r =>
        {
            r.Button("Add one", () =>
            {
                int n = ++_menuButtons;
                MainMenuButtons.Add(kit, "demo" + n, "Demo " + n, () => UiKit.Toast("Demo button " + n));
            });
            r.Button("Remove all", () =>
            {
                for (int i = MainMenuButtons.All.Count - 1; i >= 0; i--)
                {
                    if (MainMenuButtons.All[i].Id.StartsWith("demo", StringComparison.Ordinal))
                        MainMenuButtons.All[i].Remove();
                }
            });
        });
        ui.Live(() => MainMenuButtons.All.Count + " mod buttons registered", muted: true);
    }

    private static void Side(WindowDock dock)
    {
        UiKit.Open(UIKitMod.Instance, "obenuikit.demo." + dock, dock + " dock", w =>
        {
            w.Label("A column along the " + dock.ToString().ToLowerInvariant() + " edge. The scene isn't dimmed.", muted: true);
            w.Toggle("Something", true, on => { });
            w.Slider("Something else", 0f, 10f, 5f, v => { });
        }, new WindowOptions { Dock = dock, Width = 560f });
    }

    private static void Floating()
    {
        UiKit.Open(UIKitMod.Instance, "obenuikit.demo.floating", "Floating", w =>
        {
            w.Label("Drag me by the title. Click me to bring me to the front.", muted: true);
            w.Segmented("Size", new[] { "S", "M", "L" }, 1, i => { });
        }, new WindowOptions { Dock = WindowDock.Floating, Width = 600f, Height = 460f, Position = new Vector2(-420f, 120f) });
    }

    private static void Passive()
    {
        UiKit.Open(UIKitMod.Instance, "obenuikit.demo.passive", "Readout", w =>
        {
            w.Live(() => "Busy: " + UiState.IsBusy);
            w.Live(() => "Paused: " + UiState.IsPaused + ", typing: " + UiState.IsTyping);
            w.Live(() => "Time: " + Time.realtimeSinceStartup.ToString("0"));
        }, new WindowOptions
        {
            Dock = WindowDock.Left, Width = 460f, Height = 420f, LockInput = false,
            Footer = f => f.Label("Display only", muted: true),
        });
    }
}
