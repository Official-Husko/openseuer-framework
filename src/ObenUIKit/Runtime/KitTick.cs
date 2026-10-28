using ObenUIKit.Windows;
using UnityEngine;

namespace ObenUIKit.Runtime;

/// <summary>
/// The kit's only per-frame code, enabled only while a window is open: Esc closes the top window
/// (a frame later, so the same key press can't also reach the game), and open windows refresh
/// a few times a second.
/// </summary>
internal sealed class KitTick : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private WindowStack _windows;
    private bool _closePending;
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

        if (_closePending)
        {
            _closePending = false;
            _windows.Top.Close();
            return;
        }
        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && _windows.Top.CloseOnEscape)
            _closePending = true;

        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            _windows.RefreshAll();
        }
    }
}
