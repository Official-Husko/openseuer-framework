using System.Collections;
using System.Linq;
using ObenFramework;
using ObenFramework.Diagnostics;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>
/// Tells the player at the main menu, once per game start, how the framework's game check went: the game is
/// the verified one and every mod's hooks fit, or what doesn't (details on the Errors page).
/// </summary>
internal static class GameCheckNotice
{
    private const string ShownKey = "ObenUIKit.GameCheckNotice.Shown";

    private static bool _shown;
    private static bool _waiting;
    private static GameCheckResult _result;
    private static ObenMod _kit;

    public static void Init(ObenMod kit)
    {
        _kit = kit;
        _result = null;
        _waiting = false;
        ReloadState.Take(ShownKey, out _shown);
        kit.Track(Disposable.Create(() => ReloadState.Put(ShownKey, _shown)));
        GameCheck.Finished.Subscribe(kit, result =>
        {
            _result = result;
            TryShow();
        });
        GameEvents.MainMenu.Subscribe(kit, TryShow);
        if (GameCheck.Last != null)
        {
            _result = GameCheck.Last;
            TryShow();
        }
    }

    private static void TryShow()
    {
        if (_shown || _waiting || _result == null || !WorldState.InMainMenu)
            return;
        _waiting = true;
        _kit.StartCoroutine(ShowPastTitle());
    }

    // The title screen's "press any key" hides the menu; a notice there would be gone before anyone looks.
    private static IEnumerator ShowPastTitle()
    {
        while (WorldState.InMainMenu && !MenuShown())
            yield return null;
        _waiting = false;
        if (_shown || !WorldState.InMainMenu)
            yield break;
        // Let the menu's camera move settle first.
        yield return new WaitForSecondsRealtime(1f);
        _shown = true;
        UiKit.Toast(Text(_result), _result.AllFit && _result.VerifiedGame ? "Game check passed" : "Game check");
    }

    private static bool MenuShown()
    {
        var menu = MainMenuNavigationHandler.Instance;
        return menu != null && menu.gameObject.activeInHierarchy;
    }

    internal static string Text(GameCheckResult result)
    {
        if (result.Failure != null)
            return $"The game check couldn't run ({result.Failure}). The mods still load; see the log.";

        string game = $"Obenseuer {Loader.GameVersion}{(Loader.GameBuildId != null ? $" (build {Loader.GameBuildId})" : "")}";
        string version = result.VerifiedGame
            ? $"{game} is the version the mods were made for."
            : $"{game} isn't the version the framework was made for ({FrameworkInfo.VerifiedGameVersion}).";
        if (result.Problems.Count == 0)
            return $"{version} All {result.Checked} game hooks of {result.Mods} mods fit.";

        var perMod = result.Problems.GroupBy(p => p.Mod).Select(g => $"{g.Key} {g.Count()}");
        return $"{version} {result.Problems.Count} game hook{(result.Problems.Count == 1 ? "" : "s")} don't fit ({string.Join(", ", perMod)}); "
            + "the features using them may not work. Details on the MODS Errors page.";
    }
}
