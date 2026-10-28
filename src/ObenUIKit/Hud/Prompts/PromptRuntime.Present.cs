using System;
using System.Text.RegularExpressions;
using ObenFramework.NewGames;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>Putting the chosen prompt on screen and taking it off, and cleaning up after owners.</summary>
internal sealed partial class PromptRuntime
{
    private static readonly Regex KeyName = new Regex(@"\{key:([^}]+)\}", RegexOptions.Compiled);

    // Only does work when what should show changed: another prompt, new text, a rebound key, or the game
    // taking its tutorial panel back.
    private void Present(HudPrompt want)
    {
        if (_shownNative && _native.Showing && TutorialGuide.Active)
        {
            // The game started one of its own tutorial steps over ours; ours moves to the kit's panel.
            _native.Lose();
            _shownNative = false;
            _shownVersion = -1;
        }
        else if (_shownNative && !_native.Visible)
        {
            // The game's tutorial code switched the panel off under us: draw it again.
            _shownVersion = -1;
        }

        bool native = want != null && (_shownNative && want == _shown ? _native.Showing : _native.Free);
        if (want == _shown && (want == null || (want.Version == _shownVersion && native == _shownNative && want.Key.Current == _shownKey)))
            return;

        // A prompt coming back after a menu closed keeps its timer and doesn't chime again.
        bool fresh = want != _last;
        Clear();
        if (want == null)
            return;

        string text = ResolveKeys(want.Text);
        bool sound = fresh && want.Options.Sound;
        if (native && !_native.Show(text, want.Key, sound))
            native = false;
        if (!native)
            _panel.Show(_kit.Theme, text, want.Key.IsNone ? null : want.Key.Display());

        _shown = want;
        _last = want;
        _shownVersion = want.Version;
        _shownKey = want.Key.Current;
        _shownNative = native;
        if (fresh)
            want.Shown = 0f;
        if (want.Options.OnPress != null && want.Options.Capture)
        {
            try { _capture = want.Key.Capture(); }
            catch (Exception e) { _kit.Log.Error("Keeping a prompt's key from the game failed", e); }
        }
    }

    private void Clear()
    {
        if (_shown != null)
        {
            if (_shownNative)
                _native.Hide();
            else
                _panel.Hide();
        }
        _capture?.Dispose();
        _capture = null;
        _shown = null;
        _shownNative = false;
    }

    // {key:Interact} becomes the player's key the way the game's hints do it; without the game's input
    // manager (it always exists in a level) the plain name stays.
    private static string ResolveKeys(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf("{key:", StringComparison.Ordinal) < 0)
            return text ?? "";
        string resolved = HintContent.ResolveKeys(text);
        return resolved.IndexOf("{key:", StringComparison.Ordinal) < 0 ? resolved : KeyName.Replace(resolved, "$1");
    }

    private void Own(ITracker owner)
    {
        if (!_owners.Add(owner))
            return;
        owner.Track(Disposable.Create(() =>
        {
            if (Current != this)
                return;
            _owners.Remove(owner);
            for (int i = _guides.Count - 1; i >= 0; i--)
            {
                if (i < _guides.Count && _guides[i].Owner == owner)
                    _guides[i].Stop();
            }
            for (int i = _prompts.Count - 1; i >= 0; i--)
            {
                if (i < _prompts.Count && _prompts[i].Owner == owner)
                    Remove(_prompts[i]);
            }
        }));
    }

    public void Dispose()
    {
        Clear();
        foreach (var prompt in _prompts)
            prompt.IsActive = false;
        _prompts.Clear();
        _guides.Clear();
        _owners.Clear();
        _panel.Dispose();
        if (_tick != null)
            UnityEngine.Object.Destroy(_tick);
        if (_root != null)
            UnityEngine.Object.Destroy(_root.gameObject);
        if (Current == this)
            Current = null;
    }
}
