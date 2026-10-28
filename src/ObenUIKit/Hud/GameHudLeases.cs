// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
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
                current.ShowGameUI(token);
            UnityEngine.Object.Destroy(token);
        });
    }

    // A new level brings a new HUD and crosshair that know nothing of ours.
    private void Reapply()
    {
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
        if (_crosshair > 0)
            SetCrosshair(true);
        _crosshair = 0;
        if (Current == this)
            Current = null;
    }
}
