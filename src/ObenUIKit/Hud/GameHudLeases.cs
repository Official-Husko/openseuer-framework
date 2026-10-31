// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// Hides the game's crosshair or its whole HUD for as long as some mod asks. The HUD goes through the game's
/// own per-object hide list (the one the screenshot key and cutscenes use), so it stacks with the game's
/// reasons; every request is a token object of its own. Both are put back on every new level and when the
/// kit unloads.
/// </summary>
internal sealed class GameHudLeases : IDisposable
{
    private readonly List<GameObject> _hudTokens = new List<GameObject>();
    // The game's no-UI screenshot hides the HUD under its caller's object and only shows it again when the HUD
    // was visible before, so one taken while we hide it leaves that entry behind: we take it out after.
    private readonly List<GameObject> _screenshots = new List<GameObject>();
    private int _crosshair;

    public static GameHudLeases Current { get; private set; }

    public GameHudLeases(ObenMod kit)
    {
        Current = this;
        GameEvents.WhenWorldReady(kit, _ => Reapply());
    }

    public int HudHides => _hudTokens.Count;

    public int CrosshairHides => _crosshair;

    public IDisposable HideCrosshair()
    {
        if (_crosshair++ == 0)
            SetCrosshair(false);
        return Disposable.Create(() =>
        {
            if (_crosshair > 0 && --_crosshair == 0)
                SetCrosshair(true);
        });
    }

    public IDisposable HideHud()
    {
        var token = new GameObject("ObenUIKit hud hide") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(token);
        _hudTokens.Add(token);
        var ui = GameUIController.instance;
        if (ui != null)
            ui.HideGameUI(token);
        return Disposable.Create(() =>
        {
            if (!_hudTokens.Remove(token))
                return;
            var current = GameUIController.instance;
            if (current != null)
            {
                current.ShowGameUI(token);
                if (_hudTokens.Count == 0)
                    ReleaseScreenshots(current);
            }
            UnityEngine.Object.Destroy(token);
        });
    }

    /// <summary>The game is about to take a screenshot without its UI, with <paramref name="caller"/> as its hide entry.</summary>
    public void ScreenshotStarting(GameObject caller)
    {
        var ui = GameUIController.instance;
        if (_hudTokens.Count > 0 && caller != null && ui != null && !ui.GameUIVisible())
            _screenshots.Add(caller);
    }

    private void ReleaseScreenshots(GameUIController ui)
    {
        foreach (var caller in _screenshots)
        {
            if (caller != null)
                ui.ShowGameUI(caller);
        }
        _screenshots.Clear();
    }

    // A new level brings a new HUD and crosshair that know nothing of ours.
    private void Reapply()
    {
        _screenshots.Clear();
        var ui = GameUIController.instance;
        if (ui != null)
        {
            foreach (var token in _hudTokens)
                ui.HideGameUI(token);
        }
        if (_crosshair > 0)
            SetCrosshair(false);
    }

    private static void SetCrosshair(bool show)
    {
        var crosshair = Crosshair.instance;
        if (crosshair == null)
            return;
        if (show)
            crosshair.ShowCrosshair();
        else
            crosshair.HideCrosshair();
    }

    public void Dispose()
    {
        var ui = GameUIController.instance;
        foreach (var token in _hudTokens)
        {
            if (ui != null)
                ui.ShowGameUI(token);
            UnityEngine.Object.Destroy(token);
        }
        _hudTokens.Clear();
        if (ui != null)
            ReleaseScreenshots(ui);
        if (_crosshair > 0)
            SetCrosshair(true);
        _crosshair = 0;
        if (Current == this)
            Current = null;
    }
}

[HarmonyPatch(typeof(TakeScreenshot), nameof(TakeScreenshot.ScreenshotNoUI))]
internal static class ScreenshotHidePatch
{
    [HarmonyPrefix]
    private static void Before(GameObject gameObject) => GameHudLeases.Current?.ScreenshotStarting(gameObject);
}
