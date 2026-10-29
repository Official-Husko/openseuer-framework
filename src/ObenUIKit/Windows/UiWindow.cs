using System;
using System.Collections.Generic;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Input;
using ObenUIKit.Runtime;
using UnityEngine;

namespace ObenUIKit.Windows;

/// <summary>
/// An open kit window. Mods get one from <see cref="UiKit.Open(ITracker, string, string, Action{UiBuilder}, WindowOptions)"/>,
/// fill it through a <see cref="UiBuilder"/>, and it closes with Esc, its Back button, <see cref="Close()"/>,
/// or when the mod that opened it unloads. It is also a lifetime scope: whatever is passed to
/// <see cref="Track{T}"/> (or subscribed with the window as the owner) is disposed when it closes.
/// </summary>
public sealed partial class UiWindow : ITracker
{
    private readonly KitContext _kit;
    private readonly Action<UiBuilder> _build;
    private readonly WindowOptions _options;
    private readonly List<Action> _refreshers = new List<Action>();
    private readonly WindowLayout.Parts _parts;
    private readonly UiNav _nav;
    private IDisposable _inputLease;

    /// <summary>Raised once when the window closes, for any reason (see <see cref="CloseReason"/>).</summary>
    public event Action Closed;

    public string Id { get; }
    public bool IsOpen { get; private set; }

    /// <summary>Why the window closed; <see cref="WindowCloseReason.None"/> while it is open.</summary>
    public WindowCloseReason CloseReason { get; private set; }

    /// <summary>Where the window sits.</summary>
    public WindowDock Dock => _options.Dock;

    /// <summary>The window takes the player's input (see <see cref="WindowOptions.LockInput"/>).</summary>
    public bool LocksInput => _options.LockInput;

    /// <summary>Esc closes the window (the default). Turn off for windows that must be answered.</summary>
    public bool CloseOnEscape { get; set; }

    /// <summary>Keyboard and controller navigation over this window's controls.</summary>
    internal UiNav Nav => _nav;

    internal bool KeepOnLevelChange => _options.KeepOnLevelChange;

    internal KitContext Kit => _kit;

    public string Title
    {
        get => _parts.Title.text;
        set => _parts.Title.text = value;
    }

    internal UiWindow(KitContext kit, ITracker owner, string id, string title, Action<UiBuilder> build, WindowOptions options)
    {
        _kit = kit;
        _build = build;
        _options = options;
        Id = id;
        CloseOnEscape = options.CloseOnEscape;
        _parts = WindowLayout.Build(kit.Root.Rect, kit.Theme, "Window " + id, options);
        _nav = new UiNav(kit, _parts.Scroll);
        _parts.Title.text = title;
        if (_parts.Drag != null)
            _parts.Drag.Init(_parts.Column, () => kit.Windows.Raise(this));
        IsOpen = true;

        kit.Root.EnsureEventSystem();
        if (options.LockInput)
        {
            try { _inputLease = InputLock.Acquire(); }
            catch
            {
                // Nothing else knows about this window yet; don't leave its frame over the screen.
                IsOpen = false;
                if (_parts.Frame != null)
                    UnityEngine.Object.Destroy(_parts.Frame.gameObject);
                throw;
            }
        }
        owner.Track(Disposable.Create(() => Close(WindowCloseReason.OwnerUnloaded)));
        kit.Windows.Push(this);
        kit.Tick.enabled = true;
        Rebuild();
    }

    /// <summary>Clears the body and footer and builds them again with the current look and values.</summary>
    public void Rebuild()
    {
        if (!IsOpen)
            return;
        _refreshers.Clear();
        _nav.BeginRebuild();
        BeginBuildPass();
        Clear(_parts.Content);
        Clear(_parts.Footer);
        try
        {
            _build?.Invoke(new UiBuilder(_kit, this, _parts.Content));
            var footer = new UiBuilder(_kit, this, _parts.Footer, inBody: false, group: _nav.NewGroup());
            if (_options.Footer != null)
                _options.Footer(footer);
            else
                footer.Button("Back", Close);
        }
        catch (Exception e)
        {
            Fault($"Building window '{Id}' failed", e);
        }
        if (IsOpen)
            _nav.EndRebuild();
    }

    public void Close() => Close(WindowCloseReason.Requested);

    internal void Close(WindowCloseReason reason)
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        CloseReason = reason;
        _kit.Windows.Remove(this);
        // The lease count drops before the game is asked to unlock, so a throw there still closes the window.
        try { _inputLease?.Dispose(); }
        catch (Exception e) { _kit.Log.Error($"Unlocking input after window '{Id}' failed", e); }
        _inputLease = null;
        DisposeScope();
        if (_parts.Frame != null)
            UnityEngine.Object.Destroy(_parts.Frame.gameObject);
        try { Closed?.Invoke(); }
        catch (Exception e) { _kit.Log.Error($"A Closed handler of window '{Id}' threw", e); }
    }

    /// <summary>Runs <paramref name="refresh"/> a few times a second while the window is open.</summary>
    internal void AddRefresher(Action refresh) => _refreshers.Add(refresh);

    internal void Refresh()
    {
        for (int i = 0; i < _refreshers.Count && IsOpen; i++)
        {
            try { _refreshers[i](); }
            catch (Exception e)
            {
                _refreshers.RemoveAt(i--);
                Fault($"Refreshing window '{Id}' failed; that value stops updating", e);
            }
        }
    }

    /// <summary>Brings the window to the front of the kit's windows.</summary>
    internal void BringToFront()
    {
        if (_parts.Frame != null)
            _parts.Frame.SetAsLastSibling();
    }

    private static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }
}
