using System;
using System.Collections.Generic;
using System.Linq;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Input;
using ObenUIKit.Menu.Pages;
using ObenUIKit.Menu.Settings;
using ObenUIKit.Windows;

namespace ObenUIKit.Menu;

/// <summary>
/// The MODS window, reachable from the main menu and the pause menu. Other mods add pages with
/// <see cref="AddPage"/>; a page goes away when its mod unloads. The page shown is picked with a
/// cycle row at the top, the game's own way of choosing between options, or with the page keys
/// (Page Up and Page Down, the game's tab keys, the pad's bumpers).
/// </summary>
public static class ModMenu
{
    public const string WindowId = "obenuikit.modmenu";
    private const string LastPageKey = "ObenUIKit.ModMenu.Page";

    private static readonly List<ModPage> Pages = new List<ModPage>();
    private static ObenMod _kit;
    private static UiWindow _window;
    private static string _current;

    public static bool IsOpen => _window != null && _window.IsOpen;

    /// <summary>Adds a page for as long as <paramref name="owner"/> is loaded.</summary>
    public static void AddPage(ITracker owner, ModPage page)
    {
        if (page == null)
            throw new ArgumentNullException(nameof(page));
        if (Pages.Exists(p => p.Title == page.Title))
            _kit?.Log.Warn($"Two mod menu pages are called '{page.Title}'; only the first can be picked by name.");
        Pages.Add(page);
        owner.Track(Disposable.Create(() =>
        {
            Pages.Remove(page);
            if (IsOpen)
                _window.Rebuild();
        }));
        if (IsOpen)
            _window.Rebuild();
    }

    /// <summary>Opens the menu, on a given page if it exists.</summary>
    public static void Open(string page = null)
    {
        if (page != null)
            _current = page;
        if (IsOpen)
        {
            _window.Rebuild();
            return;
        }
        _window = UiKit.Open(_kit, WindowId, "Mods", Build, BuildFooter);
        _window.Pager = Turn;
        _window.Closed += () =>
        {
            _window = null;
            ModSettings.FlushAll();
            SettingsPage.ForgetSearch();
        };
    }

    public static void Close() => _window?.Close();

    internal static void Rebuild()
    {
        if (IsOpen)
            _window.Rebuild();
    }

    internal static void Init(ObenMod kit)
    {
        _kit = kit;
        Pages.Clear();
        _window = null;
        bool reopen = ReloadState.Take(LastPageKey, out _current);
        kit.Track(Disposable.Create(() =>
        {
            // A kit reload with the menu open brings it back on the same page.
            if (IsOpen)
                ReloadState.Put(LastPageKey, _current ?? "");
        }));
        if (reopen)
            kit.StartCoroutine(ReopenSoon());
    }

    private static System.Collections.IEnumerator ReopenSoon()
    {
        yield return null;
        Open(string.IsNullOrEmpty(_current) ? null : _current);
    }

    private static List<ModPage> Ordered() => Pages.OrderBy(p => p.Order).ThenBy(p => p.Title).ToList();

    private static void Build(UiBuilder ui)
    {
        ModSettings.FlushAll();
        var ordered = Ordered();
        if (ordered.Count == 0)
        {
            ui.Label("No mod has added a page yet.", muted: true);
            return;
        }

        int index = ordered.FindIndex(p => p.Title == _current);
        if (index < 0)
            index = 0;
        _current = ordered[index].Title;

        if (ordered.Count > 1)
        {
            var titles = ordered.Select(p => p.Title).ToArray();
            ui.Cycle("Page", titles, index, i =>
            {
                _current = titles[i];
                ui.Window.Rebuild();
            });
            ui.Space(10f);
        }

        // A page that throws shows a line instead of half a page, and the page row above still works.
        try
        {
            ordered[index].Build(ui);
        }
        catch (Exception e)
        {
            _kit?.Log.Error($"The mod menu page '{_current}' failed to build", e);
            ui.Label("This page couldn't be shown. The details are in the log.", muted: true);
        }
    }

    // Wraps around at either end, like the game's own tab keys.
    private static void Turn(int direction)
    {
        var ordered = Ordered();
        if (ordered.Count < 2 || !IsOpen)
            return;
        int index = Math.Max(0, ordered.FindIndex(p => p.Title == _current));
        _current = ordered[(index + direction + ordered.Count) % ordered.Count].Title;
        ModSettings.FlushAll();
        Runtime.KitRuntime.Current?.Kit.Sounds.Press();
        _window.Rebuild();
    }

    private static void BuildFooter(UiBuilder footer)
    {
        footer.Hint($"Arrows move, Enter or A picks, Backspace or X resets a setting, {NavKeys.PageKeysLabel()} turn pages.",
            UiKit.DefaultWidth - 260f);
        footer.Button("Back", Close);
    }
}
