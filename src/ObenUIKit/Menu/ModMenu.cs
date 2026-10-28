using System.Collections.Generic;
using System.Linq;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Menu.Settings;
using ObenUIKit.Windows;

namespace ObenUIKit.Menu;

/// <summary>
/// The MODS window, reachable from the main menu and the pause menu. Other mods add pages with
/// <see cref="AddPage"/>; a page goes away when its mod unloads. The page shown is picked with a
/// cycle row at the top, the game's own way of choosing between options.
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
        _window = UiKit.Open(_kit, WindowId, "Mods", Build);
        _window.Closed += () =>
        {
            _window = null;
            ModSettings.FlushAll();
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

    private static void Build(UiBuilder ui)
    {
        ModSettings.FlushAll();
        var ordered = Pages.OrderBy(p => p.Order).ThenBy(p => p.Title).ToList();
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
        ordered[index].Build(ui);
    }
}
