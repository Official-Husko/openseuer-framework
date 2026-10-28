using System;
using System.Collections;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Theme;
using ObenUIKit.Windows;
using UnityEngine;

namespace ObenUIKit.Runtime;

/// <summary>
/// Everything the kit runs on, created when the kit loads and torn down when it unloads: the
/// overlay canvas, the captured look, sounds, open windows and the tick. Nothing else in the kit
/// keeps state of its own.
/// </summary>
internal sealed class KitRuntime : IDisposable
{
    public static KitRuntime Current { get; private set; }

    public KitContext Kit { get; }

    public KitRuntime(ObenMod mod)
    {
        var root = new KitRoot(mod);
        var themes = new ThemeCapture(mod.Log);
        var windows = new WindowStack();
        var tick = root.Root.AddComponent<KitTick>();
        tick.Init(windows);
        var toasts = root.Root.AddComponent<KitToasts>();
        toasts.Init(root.Rect);
        Kit = new KitContext
        {
            Root = root,
            Themes = themes,
            Windows = windows,
            Tick = tick,
            Toasts = toasts,
            Sounds = new UiSounds(root.Root, themes),
            Log = mod.Log,
        };
        Current = this;

        // The menus only finish setting themselves up after the scene starts, so look a frame later.
        GameEvents.MainMenu.Subscribe(mod, () => mod.StartCoroutine(NextFrame(themes.CaptureMainMenu)));
        GameEvents.WhenWorldReady(mod, _ => themes.CaptureWorld());
        GameEvents.LevelChanging.Subscribe(mod, windows.CloseAll);
        if (WorldState.InMainMenu)
            themes.CaptureMainMenu();
    }

    public void Dispose()
    {
        Kit.Windows.CloseAll();
        if (Current == this)
            Current = null;
    }

    private static IEnumerator NextFrame(Action action)
    {
        yield return null;
        action();
    }
}
