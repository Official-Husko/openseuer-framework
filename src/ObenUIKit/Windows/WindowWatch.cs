using UnityEngine;

namespace ObenUIKit.Windows;

/// <summary>
/// Sits on a window's frame and closes the window (<see cref="WindowCloseReason.Destroyed"/>) when something
/// destroys the frame without closing it first, so its input lock, scope and Closed handlers still run.
/// </summary>
internal sealed class WindowWatch : MonoBehaviour
{
    public UiWindow Window;

    private void OnDestroy()
    {
        if (Window != null && Window.IsOpen)
            Window.Close(WindowCloseReason.Destroyed);
    }
}
