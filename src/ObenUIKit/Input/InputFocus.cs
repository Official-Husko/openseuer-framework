using UnityEngine;

namespace ObenUIKit.Input;

/// <summary>
/// Whether a text field or a key capture is using the keyboard. While it is, navigation keys and
/// Esc belong to it, not to the window. Esc is handled by the field in the same frame the window's
/// tick looks at it, in either order, so a frame counts as busy when something was active at any
/// point in it.
/// </summary>
internal static class InputFocus
{
    private static int _holds;
    private static int _lastActiveFrame = -1;

    public static bool Busy => _holds > 0 || _lastActiveFrame == Time.frameCount;

    public static void Hold()
    {
        _holds++;
        Touch();
    }

    public static void Release()
    {
        if (_holds > 0)
            _holds--;
        Touch();
    }

    public static void Touch() => _lastActiveFrame = Time.frameCount;
}
