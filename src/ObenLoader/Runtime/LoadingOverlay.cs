using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ObenLoader.Runtime;

/// <summary>
/// A small panel over the game window while it is still black at start: "ObenseuerExtended", what is
/// loading and a progress bar. Unity draws nothing until the first scene is up and its main thread is busy
/// loading the mods meanwhile, so the panel is a native window of its own, painted with GDI on a thread of
/// its own. It is owned by the game window (so it stays above it, also in fullscreen), never takes focus,
/// and is gone once the first scene is up. Windows calls, which Proton provides; where they fail, nothing shows.
/// </summary>
internal static partial class LoadingOverlay
{
    private const int BaseWidth = 560;
    private const int BaseHeight = 96;

    private static readonly object Gate = new object();
    private static Thread _thread;
    private static IntPtr _window;
    private static IntPtr _owner;
    private static string _status = "";
    private static int _done;
    private static int _total;
    private static bool _closing;
    private static bool _broken;

    // Sized for 1080p and scaled with the game window, so it isn't a speck on a 4K screen.
    private static float _scale = 1f;

    private static int Width => (int)(BaseWidth * _scale);
    private static int Height => (int)(BaseHeight * _scale);
    private static int S(int value) => (int)(value * _scale);

    /// <summary>Shows the panel (the first call opens it) with <paramref name="status"/>, and a bar at <paramref name="done"/> of <paramref name="total"/>.</summary>
    public static void Show(string status, int done = 0, int total = 0)
    {
        if (_broken || _closing)
            return;
        lock (Gate)
        {
            _status = status ?? "";
            _done = done;
            _total = total;
        }
        if (_thread == null)
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "ObenLoader loading panel" };
            _thread.Start();
            return;
        }
        IntPtr window = _window;
        if (window != IntPtr.Zero)
            Native.PostMessageW(window, Native.WM_APP, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>Takes the panel away.</summary>
    public static void Close()
    {
        _closing = true;
        IntPtr window = _window;
        if (window != IntPtr.Zero)
            Native.PostMessageW(window, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private static void Run()
    {
        try
        {
            _owner = WindowTitle.GameWindow();
            _window = Create();
            if (_window == IntPtr.Zero)
                return;
            if (_closing)
                Native.PostMessageW(_window, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            while (Native.GetMessageW(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                Native.TranslateMessage(ref message);
                Native.DispatchMessageW(ref message);
            }
        }
        catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is MarshalDirectiveException)
        {
            _broken = true;
        }
        finally
        {
            _window = IntPtr.Zero;
        }
    }

    private static IntPtr Create()
    {
        var instance = Native.GetModuleHandleW(null);
        var windowClass = new Native.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Procedure),
            hInstance = instance,
            hCursor = Native.LoadCursorW(IntPtr.Zero, Native.IDC_ARROW),
            lpszClassName = ClassName,
        };
        Native.RegisterClassExW(ref windowClass);
        Place(out int x, out int y);
        var window = Native.CreateWindowExW(Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE,
            ClassName, "ObenseuerExtended", Native.WS_POPUP, x, y, Width, Height, _owner, IntPtr.Zero, instance, IntPtr.Zero);
        if (window != IntPtr.Zero)
            Native.ShowWindow(window, Native.SW_SHOWNOACTIVATE);
        return window;
    }

    // Centred on the game window, a little below the middle; on the screen when there is no game window yet.
    private static void Place(out int x, out int y)
    {
        if (_owner != IntPtr.Zero && Native.GetWindowRect(_owner, out var rect) && rect.Right - rect.Left > BaseWidth)
        {
            _scale = Math.Max(1f, (rect.Bottom - rect.Top) / 1080f);
            x = rect.Left + (rect.Right - rect.Left - Width) / 2;
            y = rect.Top + (rect.Bottom - rect.Top) * 3 / 5;
            return;
        }
        _scale = Math.Max(1f, Native.GetSystemMetrics(Native.SM_CYSCREEN) / 1080f);
        x = (Native.GetSystemMetrics(Native.SM_CXSCREEN) - Width) / 2;
        y = Native.GetSystemMetrics(Native.SM_CYSCREEN) * 3 / 5;
    }
}
