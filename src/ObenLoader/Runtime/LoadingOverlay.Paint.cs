using System;

namespace ObenLoader.Runtime;

/// <summary>The panel's window procedure and painting.</summary>
internal static partial class LoadingOverlay
{
    private const string ClassName = "ObenLoaderLoadingPanel";

    // Colours as 0x00BBGGRR: the game's near-black panels, its menu red, and light grey text.
    private const uint Background = 0x00121212;
    private const uint Edge = 0x00141E8C;
    private const uint Title = 0x00DCDCDC;
    private const uint Muted = 0x00A0A0A0;
    private const uint BarBack = 0x00262626;

    // Kept in a field: the native side holds on to it for as long as the class exists.
    private static readonly Native.WndProc Procedure = WindowProcedure;

    private static IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case Native.WM_APP:
                if (!_closing)
                {
                    // The game window may have moved or changed size since the panel opened.
                    Place(out int x, out int y);
                    Native.SetWindowPos(window, Native.HWND_TOPMOST, x, y, Width, Height, Native.SWP_NOACTIVATE);
                    Native.InvalidateRect(window, IntPtr.Zero, false);
                }
                return IntPtr.Zero;
            case Native.WM_PAINT:
                Paint(window);
                return IntPtr.Zero;
            case Native.WM_MOUSEACTIVATE:
                return (IntPtr)Native.MA_NOACTIVATE;
            case Native.WM_DESTROY:
                Native.PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return Native.DefWindowProcW(window, message, wParam, lParam);
    }

    private static void Paint(IntPtr window)
    {
        string status;
        int done, total;
        lock (Gate)
        {
            status = _status;
            done = _done;
            total = _total;
        }

        IntPtr dc = Native.BeginPaint(window, out var paint);
        try
        {
            int edge = Math.Max(2, S(2));
            Fill(dc, new Native.RECT(0, 0, Width, Height), Edge);
            Fill(dc, new Native.RECT(edge, edge, Width - edge, Height - edge), Background);
            Native.SetBkMode(dc, Native.TRANSPARENT);
            Text(dc, "ObenseuerExtended", new Native.RECT(S(18), S(12), Width - S(18), S(40)), S(22), Native.FW_BOLD, Title);
            Text(dc, status, new Native.RECT(S(18), S(42), Width - S(18), S(66)), S(17), Native.FW_NORMAL, Muted);

            var bar = new Native.RECT(S(18), S(74), Width - S(18), S(82));
            Fill(dc, bar, BarBack);
            if (total > 0)
            {
                int filled = (int)((bar.Right - bar.Left) * Math.Min(1f, Math.Max(0f, done / (float)total)));
                Fill(dc, new Native.RECT(bar.Left, bar.Top, bar.Left + filled, bar.Bottom), Edge);
            }
        }
        finally
        {
            Native.EndPaint(window, ref paint);
        }
    }

    private static void Fill(IntPtr dc, Native.RECT rect, uint colour)
    {
        IntPtr brush = Native.CreateSolidBrush(colour);
        Native.FillRect(dc, ref rect, brush);
        Native.DeleteObject(brush);
    }

    private static void Text(IntPtr dc, string text, Native.RECT rect, int height, int weight, uint colour)
    {
        IntPtr font = Native.CreateFontW(-height, 0, 0, 0, weight, 0, 0, 0, Native.DEFAULT_CHARSET, 0, 0, Native.CLEARTYPE_QUALITY, 0, "Tahoma");
        IntPtr old = Native.SelectObject(dc, font);
        Native.SetTextColor(dc, colour);
        Native.DrawTextW(dc, text, -1, ref rect, Native.DT_LEFT | Native.DT_VCENTER | Native.DT_SINGLELINE | Native.DT_END_ELLIPSIS | Native.DT_NOPREFIX);
        Native.SelectObject(dc, old);
        Native.DeleteObject(font);
    }
}
