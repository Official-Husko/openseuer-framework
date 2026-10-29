using ObenFramework.Diagnostics;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Runtime;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>
/// A line in the bottom-left corner of the title screen and the main menu (across from the game's own version)
/// saying the mods are loaded and how the game check went, so it's clear the mod loader is there and working.
/// </summary>
internal static class MenuStatusLine
{
    private const float Size = 15f;

    private static TextMeshProUGUI _text;

    public static void Init(ObenMod kit)
    {
        _text = null;
        GameCheck.Finished.Subscribe(kit, _ => Refresh());
        GameEvents.MainMenu.Subscribe(kit, Refresh);
        GameEvents.SceneLoaded.Subscribe(kit, _ => Refresh());
        kit.Track(Disposable.Create(() =>
        {
            if (_text != null)
                Object.Destroy(_text.gameObject);
            _text = null;
        }));
        Refresh();
    }

    private static void Refresh()
    {
        if (!WorldState.InMainMenu)
        {
            if (_text != null)
                _text.gameObject.SetActive(false);
            return;
        }
        var kit = KitRuntime.Current?.Kit;
        if (kit == null)
            return;
        if (_text == null)
        {
            _text = UiParts.Text(kit.Root.Rect, kit.Theme, "", Size, kit.Theme.TextMuted, TextAlignmentOptions.BottomLeft);
            var rect = _text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(18f, 14f);
            rect.sizeDelta = new Vector2(900f, 60f);
        }
        _text.gameObject.SetActive(true);
        _text.text = Line();
    }

    private static string Line()
    {
        int loaded = 0, failed = 0;
        foreach (var mod in ModRegistry.Mods)
        {
            if (mod.State == ModState.Loaded)
                loaded++;
            else if (mod.State == ModState.Failed)
                failed++;
        }
        string line = $"ObenseuerExtended  ·  {loaded} mod{(loaded == 1 ? "" : "s")} loaded";
        if (failed > 0)
            line += $", {failed} failed (see Errors in MODS)";

        var result = GameCheck.Last;
        if (GameCheck.IsRunning)
            line += "  ·  checking the game...";
        else if (result == null)
            return line;
        else if (result.Failure != null)
            line += "  ·  the game check couldn't run";
        else if (result.Problems.Count > 0)
            line += $"  ·  {result.Problems.Count} game hook{(result.Problems.Count == 1 ? "" : "s")} don't fit (see Errors in MODS)";
        else if (!result.VerifiedGame)
            line += "  ·  game check passed, on a game version they weren't made for";
        else
            line += "  ·  game check passed";
        return line;
    }
}
