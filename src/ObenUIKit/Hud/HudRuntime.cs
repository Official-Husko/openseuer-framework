using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Runtime;
using ObenUIKit.Theme;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// What <see cref="Hud"/> runs on, created when the kit loads and torn down when it unloads: the hint
/// queue and where it shows (the game's hint panel in levels, the kit's own elsewhere), the subtitle
/// line and the bars. Everything a mod adds belongs to its owner and goes when the owner unloads.
/// </summary>
internal sealed class HudRuntime : IDisposable
{
    private static readonly Regex KeyName = new Regex(@"\{key:([^}]+)\}", RegexOptions.Compiled);

    private readonly KitContext _kit;
    private readonly HudTick _tick;
    private readonly RectTransform _root;
    private readonly HintQueue _queue = new HintQueue();
    private readonly NativeHints _native;
    private readonly HintPanel _panel;
    private readonly SubtitleLine _subtitle;
    private readonly BarStack _bars;
    private readonly ReadoutStack _readouts;
    private readonly HashSet<ITracker> _owners = new HashSet<ITracker>();
    private HudHint _shown;
    private bool _shownNative;

    public static HudRuntime Current { get; private set; }

    /// <summary>The look the HUD pieces are drawn with.</summary>
    public UiTheme Theme => _kit.Theme;

    public HudRuntime(KitContext kit)
    {
        _kit = kit;
        _root = UiParts.Stretch(UiParts.Child(kit.Root.Rect, "Hud"));
        // Under the windows, which are added after it.
        _root.SetAsFirstSibling();
        _native = new NativeHints(kit.Log);
        _panel = new HintPanel(_root);
        _subtitle = new SubtitleLine(_root);
        _bars = new BarStack(_root, kit.Log);
        _readouts = new ReadoutStack(_root, kit.Log);
        _tick = kit.Root.Root.AddComponent<HudTick>();
        _tick.Init(this);
        Current = this;
    }

    public HudHint AddHint(ITracker owner, string title, string text, float seconds, bool force)
    {
        Own(owner);
        var hint = _queue.Add(new HudHint(owner, title, text, seconds, force));
        Refresh();
        return hint;
    }

    public void RemoveHint(HudHint hint)
    {
        _queue.Remove(hint);
        Refresh();
    }

    public HudHint ShownHint => _shown;

    public void RemoveHints(ITracker owner)
    {
        _queue.RemoveOwner(owner);
        Refresh();
    }

    /// <summary>The player switched mod hints off: only forced ones stay.</summary>
    public void DropUnforcedHints()
    {
        _queue.RemoveUnforced();
        Refresh();
    }

    /// <summary>A hint's text changed: draw it again if it is the one showing.</summary>
    public void HintChanged(HudHint hint)
    {
        if (hint == _shown)
            Present(hint, HudGate.Blocked);
    }

    public void ShowSubtitle(string text, float seconds)
    {
        _subtitle.Show(_kit.Theme, text, seconds, HudGate.Blocked || HudGate.GameUiHidden);
        Refresh();
    }

    public HudBar AddBar(ITracker owner, string label, Func<float> value)
    {
        var bar = new HudBar(owner, label, value);
        if (!_bars.Add(bar, _kit.Theme))
            return null;
        Own(owner);
        Refresh();
        return bar;
    }

    public void RemoveBar(HudBar bar)
    {
        _bars.Remove(bar);
        Refresh();
    }

    public HudReadout AddReadout(ITracker owner, string title, Func<string> text)
    {
        var readout = new HudReadout(owner, title, text);
        if (!_readouts.Add(readout, _kit.Theme))
            return null;
        Own(owner);
        Refresh();
        return readout;
    }

    public void RemoveReadout(HudReadout readout)
    {
        _readouts.Remove(readout);
        Refresh();
    }

    /// <summary>Called every frame while there is something to show.</summary>
    public void Tick(float delta)
    {
        bool blocked = HudGate.Blocked;
        bool gameUiHidden = HudGate.GameUiHidden;
        Sync(blocked, gameUiHidden);

        // The clock only runs while the hint can be seen.
        bool visible = !blocked && (_shownNative || !gameUiHidden);
        if (_queue.Tick(delta, !visible))
            Sync(blocked, gameUiHidden);

        bool hudHidden = blocked || gameUiHidden;
        _subtitle.Tick(delta, hudHidden);
        _bars.Tick(delta, hudHidden || !WorldState.IsReady, _kit.Theme);
        _readouts.Tick(hudHidden || !WorldState.IsReady, _bars.Bottom, _kit.Theme);
        _tick.enabled = Active;
    }

    private bool Active => _queue.Count > 0 || _subtitle.Active || _bars.Count > 0 || _readouts.Count > 0;

    private void Refresh()
    {
        Sync(HudGate.Blocked, HudGate.GameUiHidden);
        _tick.enabled = Active;
    }

    // Makes what's on screen match the front of the queue: moves it when the game's hint panel appears
    // or goes away (a level change), hides it behind windows and the pause menu.
    private void Sync(bool blocked, bool gameUiHidden)
    {
        var current = _queue.Current;
        bool native = NativeHints.Available;
        if (current != _shown || (current != null && native != _shownNative))
        {
            if (_shown != null)
                HideShown();
            _shown = current;
            _shownNative = native;
            if (current != null)
                Present(current, blocked);
        }

        if (_shown == null)
            return;
        if (_shownNative)
            _native.SetBlocked(blocked);
        else
            _panel.SetVisible(!blocked && !gameUiHidden);
    }

    private void Present(HudHint hint, bool blocked)
    {
        if (_shownNative)
        {
            _native.Show(hint, blocked);
            return;
        }
        _panel.Show(_kit.Theme, hint.Title, ResolveKeys(hint.Text));
    }

    private void HideShown()
    {
        if (_shownNative)
            _native.Hide();
        else
            _panel.Hide();
        _shown = null;
    }

    // The game turns {key:Interact} into the player's key; without its input manager the name is shown plainly.
    private static string ResolveKeys(string text)
    {
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
            _queue.RemoveOwner(owner);
            _bars.RemoveOwner(owner);
            _readouts.RemoveOwner(owner);
            Refresh();
        }));
    }

    internal static string NameOf(ITracker owner)
    {
        if (owner is ObenMod mod)
            return mod.Info.Manifest.DisplayName;
        if (owner is ObenFramework.Features.ModFeature feature && feature.Owner != null)
            return feature.Owner.Info.Manifest.DisplayName;
        return "Hint";
    }

    public void Dispose()
    {
        if (Current == this)
            Current = null;
        _queue.Clear();
        _native.Dispose();
        _panel.Dispose();
        _subtitle.Dispose();
        _bars.Dispose();
        _readouts.Dispose();
        _owners.Clear();
        if (_tick != null)
            UnityEngine.Object.Destroy(_tick);
        if (_root != null)
            UnityEngine.Object.Destroy(_root.gameObject);
    }
}
