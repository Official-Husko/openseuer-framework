using System;
using System.Collections.Generic;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Input;
using ObenUIKit.Runtime;
using UnityEngine;

namespace ObenUIKit.Windows;

/// <summary>
/// An open kit window. Mods get one from <see cref="UiKit.Open"/>, fill it through a
/// <see cref="UiBuilder"/>, and it closes with Esc, its Back button, <see cref="Close"/>, or when
/// the mod that opened it unloads.
/// </summary>
public sealed class UiWindow
{
    private readonly KitContext _kit;
    private readonly Action<UiBuilder> _build;
    private readonly Action<UiBuilder> _footer;
    private readonly List<Action> _refreshers = new List<Action>();
    private WindowLayout.Parts _parts;
    private IDisposable _inputLease;
    private readonly UiNav _nav;

    /// <summary>Raised once when the window closes, for any reason.</summary>
    public event Action Closed;

    public string Id { get; }
    public bool IsOpen { get; private set; }

    /// <summary>Esc closes the window (the default). Turn off for windows that must be answered.</summary>
    public bool CloseOnEscape { get; set; } = true;

    /// <summary>Keyboard and controller navigation over this window's controls.</summary>
    internal UiNav Nav => _nav;

    public string Title
    {
        get => _parts.Title.text;
        set => _parts.Title.text = value;
    }

    internal UiWindow(KitContext kit, ITracker owner, string id, string title, Action<UiBuilder> build,
        Action<UiBuilder> footer, float width, float height)
    {
        _kit = kit;
        _build = build;
        _footer = footer;
        Id = id;
        _parts = WindowLayout.Build(kit.Root.Rect, kit.Theme, "Window " + id, width, height);
        _nav = new UiNav(kit, _parts.Scroll);
        _parts.Title.text = title;
        IsOpen = true;

        kit.Root.EnsureEventSystem();
        _inputLease = InputLock.Acquire();
        owner.Track(Disposable.Create(Close));
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
        Clear(_parts.Content);
        Clear(_parts.Footer);
        try
        {
            _build?.Invoke(new UiBuilder(_kit, this, _parts.Content));
            var footer = new UiBuilder(_kit, this, _parts.Footer, inBody: false, group: _nav.NewGroup());
            if (_footer != null)
                _footer(footer);
            else
                footer.Button("Back", Close);
        }
        catch (Exception e)
        {
            _kit.Log.Error($"Building window '{Id}' failed", e);
        }
        _nav.EndRebuild();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _kit.Windows.Remove(this);
        _inputLease?.Dispose();
        _inputLease = null;
        if (_parts.Frame != null)
            UnityEngine.Object.Destroy(_parts.Frame.gameObject);
        try { Closed?.Invoke(); }
        catch (Exception e) { _kit.Log.Error($"A Closed handler of window '{Id}' threw", e); }
    }

    /// <summary>Runs <paramref name="refresh"/> a few times a second while the window is open.</summary>
    internal void AddRefresher(Action refresh) => _refreshers.Add(refresh);

    internal void Refresh()
    {
        for (int i = 0; i < _refreshers.Count; i++)
        {
            try { _refreshers[i](); }
            catch (Exception e)
            {
                _kit.Log.Error($"Refreshing window '{Id}' failed; that value stops updating", e);
                _refreshers.RemoveAt(i--);
            }
        }
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
