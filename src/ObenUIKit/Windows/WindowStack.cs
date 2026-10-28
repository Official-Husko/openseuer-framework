using System.Collections.Generic;

namespace ObenUIKit.Windows;

/// <summary>Open kit windows, newest on top.</summary>
internal sealed class WindowStack
{
    private readonly List<UiWindow> _open = new List<UiWindow>();

    public bool Any => _open.Count > 0;

    public UiWindow Top => _open.Count > 0 ? _open[_open.Count - 1] : null;

    public IReadOnlyList<UiWindow> Open => _open;

    public void Push(UiWindow window) => _open.Add(window);

    public void Remove(UiWindow window) => _open.Remove(window);

    public void RefreshAll()
    {
        for (int i = 0; i < _open.Count; i++)
            _open[i].Refresh();
    }

    public void CloseAll()
    {
        for (int i = _open.Count - 1; i >= 0; i--)
            _open[i].Close();
    }
}
