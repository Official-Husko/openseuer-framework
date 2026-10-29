using ObenUIKit.Input;
using ObenUIKit.Windows;
using UnityEngine;

namespace ObenUIKit.Runtime;

/// <summary>
/// The kit's only per-frame code, enabled only while a window is open: Esc or the pad's B closes
/// the top window (a frame later, so the same key press can't also reach the game, and not while a
/// text field or key capture has the keyboard), the top window gets keyboard and controller
/// navigation and, when it has pages, the page keys, and open windows refresh a few times a second.
/// </summary>
internal sealed class KitTick : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private WindowStack _windows;
    private bool _closePending;
    private UiWindow _pendingClose;
    private float _nextRefresh;

    public void Init(WindowStack windows)
    {
        _windows = windows;
        enabled = false;
    }

    private void Update()
    {
        if (_windows == null || !_windows.Any)
        {
            enabled = false;
            return;
        }

        var top = _windows.TopModal;
        if (_closePending)
        {
            _closePending = false;
            if (top != null && top == _pendingClose)
                top.Close(WindowCloseReason.Escape);
            _pendingClose = null;
            return;
        }

        bool typing = InputFocus.Busy;
        if (top != null && !typing && top.CloseOnEscape && NavKeys.Back())
        {
            _closePending = true;
            _pendingClose = top;
        }
        else if (top != null && !typing)
        {
            top.Nav.Tick(Time.unscaledTime);
            if (top.Pager != null && top.IsOpen && NavKeys.Page() is int page && page != 0)
                top.Run("Turning the page", () => top.Pager(page));
        }

        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            _windows.RefreshAll();
        }
    }
}
