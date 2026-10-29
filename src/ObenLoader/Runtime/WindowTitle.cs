using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ObenLoader.Runtime;

/// <summary>
/// Puts "ObenseuerExtended v&lt;version&gt;" after the game's own window title ("Obenseuer - ObenseuerExtended
/// v0.1.0"), so it is clear the game runs modded. Windows calls, which Proton provides; where they don't work,
/// nothing happens. The start-up progress is on the <see cref="LoadingOverlay"/>, which shows in fullscreen too.
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

    private static bool _broken;

    /// <summary>
    /// Adds " - ObenseuerExtended v&lt;version&gt;" to the game's title, once: calling it again (after something
    /// reset the title) adds it back without doubling it.
    /// </summary>
    public static void Brand(string version)
    {
        if (_broken)
            return;
        try
        {
            var window = Find();
            if (window == IntPtr.Zero)
                return;
            string brand = " - ObenseuerExtended v" + version;
            string title = Read(window);
            if (!title.EndsWith(brand, StringComparison.Ordinal))
                SetWindowTextW(window, title + brand);
        }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is MarshalDirectiveException)
        {
            _broken = true;
        }
    }

    /// <summary>The window Unity made for this process, or zero when there is none (yet) or the calls don't work.</summary>
    public static IntPtr GameWindow()
    {
        try { return Find(); }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is MarshalDirectiveException) { return IntPtr.Zero; }
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
