using System;
using System.Collections.Generic;
using ObenUIKit.Runtime;

namespace ObenUIKit.Computer;

/// <summary>The app windows open on computer screens, one per app and screen.</summary>
internal static class ComputerWindows
{
    private static readonly List<ComputerWindow> Windows = new List<ComputerWindow>();

    public static IReadOnlyList<ComputerWindow> All
    {
        get
        {
            Prune();
            return Windows;
        }
    }

    public static ComputerWindow Find(string appId, ComputerScreen screen)
    {
        Prune();
        foreach (var window in Windows)
        {
            if (window.AppId == appId && window.Screen == screen)
                return window;
        }
        return null;
    }

    public static bool AnyOpen(string appId)
    {
        Prune();
        return Windows.Exists(w => w.AppId == appId);
    }

    /// <summary>Opens the app on the screen, or brings its window back up when it is there already.</summary>
    public static ComputerWindow Show(ComputerApp app, ComputerScreen screen)
    {
        var window = Find(app.Id, screen);
        if (window != null)
        {
            window.Restore();
            return window;
        }

        window = ComputerWindow.Create(app, screen);
        Windows.Add(window);
        var ui = new ComputerUi(window, app.Draw);
        try
        {
            ui.Rebuild();
        }
        catch (Exception e)
        {
            KitRuntime.Current?.Kit.Log.Error($"The computer app '{app.Id}' failed to draw its window", e);
            // It never opened, so no Closed either.
            Windows.Remove(window);
            window.Destroy();
            return null;
        }
        window.Restore();
        ComputerApps.Opened.Raise(app.Id);
        return window;
    }

    public static void Close(ComputerWindow window)
    {
        if (!Windows.Remove(window) || window.Closing)
            return;
        Finish(window);
    }

    public static void CloseApp(string appId)
    {
        foreach (var window in Windows.ToArray())
        {
            if (window.AppId == appId)
                Close(window);
        }
    }

    public static void CloseAll()
    {
        foreach (var window in Windows.ToArray())
            Close(window);
    }

    /// <summary>Windows whose screen went with the level say so (their Closed events run).</summary>
    internal static void Prune()
    {
        for (int i = Windows.Count - 1; i >= 0; i--)
        {
            var window = Windows[i];
            if (window.IsOpen)
                continue;
            Windows.RemoveAt(i);
            if (!window.Closing)
                Finish(window);
        }
    }

    private static void Finish(ComputerWindow window)
    {
        try { window.Destroy(); }
        catch (Exception e) { KitRuntime.Current?.Kit.Log.Error($"A Closed handler of the computer app '{window.AppId}' threw", e); }
        ComputerApps.Closed.Raise(window.AppId);
    }
}
