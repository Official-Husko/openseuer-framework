using System;
using ObenLoader.Logging;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// Shows a hint through the game's own <c>HintOverlay</c> side panel, so it looks and animates like
/// the game's hints and <c>{key:Action}</c> names turn into the player's current binding. The overlay
/// shows one entry at a time (the highest priority, then the newest), so ours goes above the scene's own
/// hints while it's up and they come back after. Available in levels only.
/// </summary>
internal sealed class NativeHints
{
    // Above the hints the game attaches to scene objects.
    private const int Priority = 100;

    private readonly ModLogger _log;
    private GameObject _token;
    private bool _showing;
    private bool _suppressed;

    public NativeHints(ModLogger log)
    {
        _log = log;
    }

    public static bool Available => HintOverlay.instance != null;

    public void Show(HudHint hint, bool blocked)
    {
        try
        {
            _token ??= CreateToken();
            var content = new HintContent(hint.Title).AddText(null, hint.Text);
            HintOverlay.Show(_token, content, Priority);
            if (hint.Force)
                HintOverlay.SetHiddenOverride(_token, false);
            _showing = true;
            _suppressed = false;
            SetBlocked(blocked);
        }
        catch (Exception e)
        {
            _log.Error("The game's hint panel failed, hints won't show in it", e);
        }
    }

    /// <summary>Hides the panel while a kit window or the pause menu is up, and brings it back after.</summary>
    public void SetBlocked(bool blocked)
    {
        if (!_showing || blocked == _suppressed)
            return;
        _suppressed = blocked;
        try
        {
            HintOverlay.SetSuppressed(_token, blocked);
        }
        catch (Exception e)
        {
            _log.Error("Couldn't hide the game's hint panel", e);
        }
    }

    public void Hide()
    {
        if (_showing && _token != null)
            HintOverlay.Hide(_token);
        _showing = false;
        _suppressed = false;
    }

    public void Dispose()
    {
        Hide();
        if (_token != null)
            UnityEngine.Object.Destroy(_token);
        _token = null;
    }

    // The overlay wants a Unity object as the entry's owner and forgets the entry when it is destroyed.
    private static GameObject CreateToken()
    {
        var token = new GameObject("ObenUIKit hint") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.Object.DontDestroyOnLoad(token);
        return token;
    }
}
