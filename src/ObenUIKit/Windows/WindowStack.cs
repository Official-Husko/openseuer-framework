using System.Collections.Generic;
using ObenFramework.Ui;

namespace ObenUIKit.Windows;

/// <summary>
/// Open kit windows, newest on top. Windows that lock input are the modal ones: the top modal window
/// gets Esc and keyboard navigation, and while one is open the HUD and hotkeys step aside. Display-only
/// windows (<see cref="WindowOptions.LockInput"/> off) just sit there.
/// </summary>
internal sealed class WindowStack
{
    private readonly List<UiWindow> _open = new List<UiWindow>();
    private int _modal;

    /// <summary>Any window at all, modal or not.</summary>
    public bool Any => _open.Count > 0;

    /// <summary>A window that takes the player's input is open.</summary>
    public bool AnyModal => _modal > 0;

    /// <summary>The newest window that takes input, or null.</summary>
    public UiWindow TopModal
    {
        get
        {
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                if (_open[i].LocksInput)
                    return _open[i];
            }
            return null;
        }
    }

    public IReadOnlyList<UiWindow> Open => _open;

    public void Push(UiWindow window)
    {
        _open.Add(window);
        if (window.LocksInput)
            _modal++;
        UiState.Changed();
    }

    public void Remove(UiWindow window)
    {
        if (!_open.Remove(window))
            return;
        if (window.LocksInput)
            _modal--;
        UiState.Changed();
    }

    /// <summary>Moves a window to the top (a floating window the player clicked).</summary>
    public void Raise(UiWindow window)
    {
        if (!_open.Remove(window))
            return;
        _open.Add(window);
        window.BringToFront();
    }

    public void RefreshAll()
    {
        for (int i = 0; i < _open.Count; i++)
            _open[i].Refresh();
    }

    public void CloseAll(WindowCloseReason reason = WindowCloseReason.Requested)
    {
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            if (i < _open.Count)
                _open[i].Close(reason);
        }
    }

    /// <summary>The player is leaving the level: close everything that didn't ask to stay.</summary>
    public void CloseForLevelChange()
    {
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            if (i < _open.Count && !_open[i].KeepOnLevelChange)
                _open[i].Close(WindowCloseReason.LevelChange);
        }
    }
}
