using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ObenLoader.Runtime;

/// <summary>
/// The game window stays black while the loader loads the mods and Unity loads the first scene, since no frame is
/// drawn until then. Its title says what is going on meanwhile ("Obenseuer - loading mods (3/9): ObenFramework"),
/// so it doesn't look stuck, and goes back to the game's own title once the first scene is up. Windows calls,
/// which Proton provides; where they don't work, nothing happens.
/// </summary>
internal static class WindowTitle
{
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetWindowTextW(IntPtr window, string text);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();

    private const string UnityWindowClass = "UnityWndClass";

    private static IntPtr _window;
    private static string _title;
    private static bool _broken;

    /// <summary>Shows "&lt;game's title&gt; - <paramref name="status"/>" in the window title.</summary>
    public static void Show(string status)
    {
        if (_broken)
            return;
        try
        {
            if (_window == IntPtr.Zero)
            {
                _window = Find();
                if (_window == IntPtr.Zero)
                    return;
                _title = Read(_window);
            }
            SetWindowTextW(_window, string.IsNullOrEmpty(_title) ? status : _title + " - " + status);
        }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is MarshalDirectiveException)
        {
            _broken = true;
        }
    }

    /// <summary>Puts the game's own title back.</summary>
    public static void Restore()
    {
        if (_broken || _window == IntPtr.Zero || _title == null)
            return;
        try { SetWindowTextW(_window, _title); }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) { }
        _window = IntPtr.Zero;
    }

    // The window Unity made for this process (it can exist before it is the active one).
    private static IntPtr Find()
    {
        var active = GetActiveWindow();
        if (active != IntPtr.Zero && ClassOf(active) == UnityWindowClass)
            return active;
        uint self = GetCurrentProcessId();
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != self || ClassOf(window) != UnityWindowClass)
                return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static string ClassOf(IntPtr window)
    {
        var name = new StringBuilder(64);
        GetClassNameW(window, name, name.Capacity);
        return name.ToString();
    }

    private static string Read(IntPtr window)
    {
        var text = new StringBuilder(256);
        GetWindowTextW(window, text, text.Capacity);
        return text.ToString();
    }
}
