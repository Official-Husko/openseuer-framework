// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Windows;
using UnityEngine;

namespace ObenUIKit.Computer;

/// <summary>A registered app.</summary>
public sealed class ComputerApp
{
    internal ComputerApp(ITracker owner, string id, string title, Sprite icon, Action<UiBuilder> build, Action<ComputerUi> draw,
        float width, float height)
    {
        Owner = owner;
        Id = id;
        Title = title;
        Icon = icon;
        Build = build;
        Draw = draw;
        Width = width;
        Height = height;
    }

    internal ITracker Owner { get; }
    public string Id { get; }
    public string Title { get; }
    public Sprite Icon { get; }

    /// <summary>It opens in a window on the computer's own screen (<see cref="ComputerApps.RegisterOnScreen"/>), not in a kit window.</summary>
    public bool OnScreen => Draw != null;

    internal Action<UiBuilder> Build { get; }
    internal Action<ComputerUi> Draw { get; }
    internal float Width { get; }
    internal float Height { get; }
}

/// <summary>
/// Apps for the in-game computers. A registered app gets an icon on the desktop of every computer; a double
/// click opens it. An app from <see cref="Register"/> opens in a kit window over the screen, and can also be
/// opened anywhere with <see cref="Open"/> (a standalone app window), say from a hotkey or an item. An app from
/// <see cref="RegisterOnScreen"/> opens in a window on the computer's own screen, like the e-mail. Apps go away
/// when their mod unloads.
/// </summary>
public static class ComputerApps
{
    private const int MaxApps = 30;
    private static readonly List<ComputerApp> Apps = new List<ComputerApp>();
    private static readonly Dictionary<string, UiWindow> OpenWindows = new Dictionary<string, UiWindow>();

    /// <summary>An app window opened (the app's id).</summary>
    public static readonly AppEvent Opened = new AppEvent("ComputerApps.Opened");

    /// <summary>An app window closed (the app's id).</summary>
    public static readonly AppEvent Closed = new AppEvent("ComputerApps.Closed");

    /// <summary>The frame an app window last closed; the computer ignores its keys until the next frame.</summary>
    internal static int ClosedFrame { get; private set; } = -10;

    public static IReadOnlyList<ComputerApp> All => Apps;

    /// <summary>
    /// Registers an app until <paramref name="owner"/> unloads or the result is disposed. <paramref name="build"/>
    /// fills its window like any kit window. Throws when the id is taken.
    /// </summary>
    /// <param name="id">Unique name, e.g. "MyMod.Stocks".</param>
    /// <param name="icon">Desktop icon; null uses the screen's text file icon.</param>
    public static IDisposable Register(ITracker owner, string id, string title, Action<UiBuilder> build, Sprite icon = null,
        float width = UiKit.DefaultWidth, float height = 0f)
    {
        if (build == null)
            throw new ArgumentNullException(nameof(build));
        return Add(owner, new ComputerApp(owner, id, title, icon, build, null, width, height));
    }

    /// <summary>
    /// Registers an app that opens in a window on the computer's own screen (title bar, minimise and close
    /// buttons, a taskbar entry), until <paramref name="owner"/> unloads or the result is disposed.
    /// <paramref name="draw"/> fills the window with <see cref="ComputerUi"/>; it runs again on
    /// <see cref="ComputerUi.Rebuild"/>. Each computer gets its own window. Throws when the id is taken.
    /// </summary>
    public static IDisposable RegisterOnScreen(ITracker owner, string id, string title, Action<ComputerUi> draw, Sprite icon = null)
    {
        if (draw == null)
            throw new ArgumentNullException(nameof(draw));
        return Add(owner, new ComputerApp(owner, id, title, icon, null, draw, 0f, 0f));
    }

    private static IDisposable Add(ITracker owner, ComputerApp app)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        string id = app.Id, title = app.Title;
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
            throw new ArgumentException("An app needs an id of 1 to 64 characters.", nameof(id));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("An app needs a title.", nameof(title));
        if (Find(id) != null)
            throw new ArgumentException($"An app '{id}' is already registered.", nameof(id));
        if (Apps.Count >= MaxApps)
            throw new InvalidOperationException($"At most {MaxApps} apps fit on a desktop.");

        Apps.Add(app);
        ComputerDesk.Refresh();
        var registration = Disposable.Create(() => Remove(app));
        owner.Track(registration);
        return registration;
    }

    public static ComputerApp Find(string id)
    {
        foreach (var app in Apps)
        {
            if (app.Id == id)
                return app;
        }
        return null;
    }

    /// <summary>
    /// Opens an app's window (or brings back the open one). An on-screen app opens on the computer the player is
    /// using. False when there is no such app, or for an on-screen app when no computer is in use.
    /// </summary>
    public static bool Open(string id)
    {
        var app = Find(id);
        if (app == null)
            return false;
        if (app.OnScreen)
        {
            var screen = ObenFramework.World.Computers.ScreenOf(ObenFramework.World.Computers.Current);
            return screen != null && ComputerWindows.Show(app, screen) != null;
        }
        if (OpenWindows.TryGetValue(id, out var open) && open.IsOpen)
            return true;
        var window = UiKit.Open(app.Owner, "ComputerApp." + id, app.Title, app.Build, width: app.Width, height: app.Height);
        OpenWindows[id] = window;
        window.Closed += () =>
        {
            OpenWindows.Remove(id);
            ClosedFrame = Time.frameCount;
            Closed.Raise(id);
        };
        Opened.Raise(id);
        return true;
    }

    /// <summary>The app opened from a desktop icon of <paramref name="screen"/>.</summary>
    internal static void OpenOn(ComputerScreen screen, string id)
    {
        var app = Find(id);
        if (app == null)
            return;
        if (app.OnScreen)
            ComputerWindows.Show(app, screen);
        else
            Open(id);
    }

    /// <summary>Its kit window is open, or (on-screen apps) a window on any computer, minimised or not.</summary>
    public static bool IsOpen(string id) =>
        (OpenWindows.TryGetValue(id, out var window) && window.IsOpen) || ComputerWindows.AnyOpen(id);

    /// <summary>The windows of on-screen apps, on every computer of the level.</summary>
    public static IReadOnlyList<ComputerWindow> ScreenWindows => ComputerWindows.All;

    /// <summary>Closes the app's kit window and its windows on computer screens.</summary>
    public static void Close(string id)
    {
        if (OpenWindows.TryGetValue(id, out var window))
            window.Close();
        ComputerWindows.CloseApp(id);
    }

    internal static void Init(ObenMod kit)
    {
        ComputerDesk.Init(kit);
        // Windows on screens close with their level: before a level change, or once a load has replaced it.
        GameEvents.LevelChanging.Subscribe(kit, ComputerWindows.CloseAll);
        GameEvents.SceneLoaded.Subscribe(kit, () => ComputerWindows.Prune());
        kit.Track(Disposable.Create(() =>
        {
            foreach (var window in new List<UiWindow>(OpenWindows.Values))
                window.Close();
            OpenWindows.Clear();
            ComputerWindows.CloseAll();
            Apps.Clear();
        }));
    }

    private static void Remove(ComputerApp app)
    {
        if (!Apps.Remove(app))
            return;
        Close(app.Id);
        ComputerDesk.Refresh();
    }
}

/// <summary>An app event: subscribe with an owner so a hot reload removes the handler.</summary>
public sealed class AppEvent
{
    private readonly ObenFramework.Events.HandlerList<Action<string>> _handlers = new ObenFramework.Events.HandlerList<Action<string>>();
    private readonly string _name;
    private string _current;
    private readonly Action<Action<string>> _call;

    internal AppEvent(string name)
    {
        _name = name;
        _call = h => h(_current);
    }

    public void Subscribe(ITracker owner, Action<string> handler) => _handlers.Add(owner, handler);

    public IDisposable Subscribe(Action<string> handler) => _handlers.Add(handler);

    internal void Raise(string appId)
    {
        var kit = UIKitMod.Instance;
        if (kit == null)
            return;
        _current = appId;
        _handlers.ForEach(_call, kit.Log, _name);
    }
}
