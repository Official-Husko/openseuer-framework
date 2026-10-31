// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.World;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ObenUIKit.Computer;

/// <summary>
/// Puts the registered apps on the desktops of the in-game computers. Each app gets a copy of the screen's own
/// Recycle Bin icon (its picture, title and hover look) in the desktop's icon grid, and like it opens on a double
/// click. Icons are added to every computer of a level once it's ready, and again when apps come or go.
/// </summary>
internal static class ComputerDesk
{
    private const string IconPrefix = "Mod app ";

    private static readonly List<ComputerScreen> Screens = new List<ComputerScreen>();
    private static bool _warned;

    public static void Init(ObenMod kit)
    {
        Screens.Clear();
        GameEvents.WorldReady.Subscribe(kit, () => Refresh());
        Computers.Used.Subscribe(kit, use =>
        {
            if (use.InUse)
                Decorate(Computers.ScreenOf(use.Computer));
        });
        kit.Track(Disposable.Create(() =>
        {
            foreach (var screen in Screens)
                Clear(screen, keep: null);
            Screens.Clear();
        }));
        Refresh();
    }

    /// <summary>Lays the icons out again on every computer of the level (an app came or went).</summary>
    public static void Refresh()
    {
        Screens.RemoveAll(s => s == null);
        foreach (var screen in Screens)
            Layout(screen);
        foreach (var computer in Computers.All())
            Decorate(Computers.ScreenOf(computer));
    }

    private static void Decorate(ComputerScreen screen)
    {
        if (screen == null)
            return;
        if (!Screens.Contains(screen))
            Screens.Add(screen);
        Layout(screen);
    }

    private static void Layout(ComputerScreen screen)
    {
        var grid = ComputerScreenParts.Shortcuts(screen);
        var template = ComputerScreenParts.DesktopIcon(screen);
        if (grid == null || template == null)
        {
            if (!_warned)
                KitRuntime.Current?.Kit.Log.Warn("A computer's desktop has no icon grid or Recycle Bin icon to copy; mod apps can't show on it.");
            _warned = true;
            return;
        }

        var apps = ComputerApps.All;
        var keep = new HashSet<string>();
        foreach (var app in apps)
        {
            keep.Add(IconPrefix + app.Id);
            var icon = grid.Find(IconPrefix + app.Id);
            if (icon == null)
                icon = Icon(screen, template, grid, app);
            // The game's own shortcuts first, then the apps in the order they were registered.
            icon.SetAsLastSibling();
        }
        Clear(screen, keep);
    }

    // Removes icons of apps that are gone (all of them without a keep set).
    private static void Clear(ComputerScreen screen, HashSet<string> keep)
    {
        var grid = screen != null ? ComputerScreenParts.Shortcuts(screen) : null;
        if (grid == null)
            return;
        for (int i = grid.childCount - 1; i >= 0; i--)
        {
            var child = grid.GetChild(i);
            if (child.name.StartsWith(IconPrefix, StringComparison.Ordinal) && (keep == null || !keep.Contains(child.name)))
            {
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
        }
    }

    private static Transform Icon(ComputerScreen screen, GameObject template, RectTransform grid, ComputerApp app)
    {
        var copy = Object.Instantiate(template, grid, false);
        copy.name = IconPrefix + app.Id;
        // The copy must not open the Recycle Bin: its handler goes, its click sound stays.
        var program = copy.GetComponent<TaskbarProgram>();
        var button = copy.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            ComputerScreenParts.Mute(button, program);
            string id = app.Id;
            button.onClick.AddListener(() => Clicked(screen, copy, id));
        }
        if (program != null)
            Object.Destroy(program);

        foreach (var image in copy.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject != copy)
            {
                image.sprite = app.Icon != null ? app.Icon : ComputerScreenParts.DefaultIcon(screen);
                image.preserveAspect = true;
                break;
            }
        }
        foreach (var title in copy.GetComponentsInChildren<TextMeshProUGUI>(true))
            title.text = app.Title;
        copy.SetActive(true);
        return copy.transform;
    }

    private static void Clicked(ComputerScreen screen, GameObject icon, string id)
    {
        var clicks = DoubleClickController.instance;
        if (clicks != null && !clicks.DoubleClick(icon))
            return;
        try { ComputerApps.OpenOn(screen, id); }
        catch (Exception e) { KitRuntime.Current?.Kit.Log.Error($"The computer app '{id}' failed to open", e); }
    }
}
