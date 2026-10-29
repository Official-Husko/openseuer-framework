// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Game;
using ObenFramework.NewGames;
using ObenLoader.Runtime;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;

namespace ObenUIKit.NewGameScreen;

/// <summary>
/// Adds "Mod options" to the New Game screen's difficulty page when mods registered options. It's a copy
/// of the page's own Back button (so it has the game's font, colours and sounds) placed just above it,
/// with the copy's Relay objects removed like the pause menu entry does. The copy lives in the menu scene
/// and is removed when the kit unloads. It is hidden while no mod has an option.
/// </summary>
internal static class NewGameButton
{
    public const string ObjectName = "Mod options (ObenUIKit)";

    private static readonly AccessTools.FieldRef<MainMenuNavigationHandler, MainCharacterMenuDifficultyNavigationHelper> HandlerHelper =
        AccessTools.FieldRefAccess<MainMenuNavigationHandler, MainCharacterMenuDifficultyNavigationHelper>("characterMenuDifficultyNavigationHelper");
    private static readonly AccessTools.FieldRef<MainCharacterMenuDifficultyNavigationHelper, UnityEngine.UI.Button> BackButton =
        AccessTools.FieldRefAccess<MainCharacterMenuDifficultyNavigationHelper, UnityEngine.UI.Button>("back");

    private static GameObject _entry;
    private static bool _tracked;

    /// <summary>
    /// Called when a difficulty page shows: makes the button once per menu scene, then shows or hides it. The pause
    /// menu has a difficulty page too, without the New Game screen around it; that one is left alone.
    /// </summary>
    public static void Show(DifficultyUI page)
    {
        var kit = UIKitMod.Instance;
        if (kit == null || !WorldState.InMainMenu)
            return;
        if (!_tracked)
        {
            _tracked = true;
            kit.Track(Disposable.Create(Remove));
            NewGameOptions.ListChanged.Subscribe(kit, UpdateVisible);
        }
        if (_entry == null)
            _entry = Make(page);
        UpdateVisible();
    }

    private static GameObject Make(DifficultyUI page)
    {
        var helper = FindHelper(page);
        var back = helper != null ? BackButton(helper) : null;
        if (back == null)
        {
            UIKitMod.Instance.Log.Warn("The New Game screen's Back button wasn't found; the Mod options button is not shown.");
            return null;
        }

        var clone = UnityEngine.Object.Instantiate(back.gameObject, back.transform.parent, false);
        clone.name = ObjectName;
        Strip(clone);
        Place(clone.GetComponent<RectTransform>(), back.GetComponent<RectTransform>());

        var text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = "Mod options";
        var button = clone.GetComponent<UnityEngine.UI.Button>();
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener(Open);
        return clone;
    }

    // Right above Back. When the buttons sit in a layout group, the group places it next to Back instead.
    private static void Place(RectTransform clone, RectTransform back)
    {
        clone.SetSiblingIndex(back.GetSiblingIndex());
        if (back.parent != null && back.parent.GetComponent<UnityEngine.UI.LayoutGroup>() != null)
            return;
        clone.anchoredPosition = back.anchoredPosition + new Vector2(0f, back.rect.height * 1.2f);
    }

    private static void Strip(GameObject clone)
    {
        var effect = clone.GetComponent<TextButtonColorEffectTMP>();
        if (effect != null)
        {
            Native.ButtonOnSelected(effect) = null;
            Native.ButtonOnUnselected(effect) = null;
        }
        foreach (var relay in clone.GetComponentsInChildren<Relay>(true))
            UnityEngine.Object.DestroyImmediate(relay);
    }

    private static void Open()
    {
        try
        {
            var kit = UIKitMod.Instance;
            if (kit != null)
                NewGameOptionsWindow.Open(kit);
        }
        catch (Exception e)
        {
            UIKitMod.Instance?.Log.Error("Opening the new game options failed", e);
        }
    }

    private static void UpdateVisible()
    {
        if (_entry != null)
            _entry.SetActive(NewGameOptions.Options.Count > 0);
    }

    private static MainCharacterMenuDifficultyNavigationHelper FindHelper(DifficultyUI page)
    {
        var handler = MainMenuNavigationHandler.Instance;
        var helper = handler != null ? HandlerHelper(handler) : null;
        if (helper != null)
            return helper;
        return page != null ? page.GetComponentInParent<MainCharacterMenuDifficultyNavigationHelper>(true) : null;
    }

    private static void Remove()
    {
        _tracked = false;
        if (_entry == null)
            return;
        _entry.SetActive(false);
        _entry.transform.SetParent(null, false);
        UnityEngine.Object.Destroy(_entry);
        _entry = null;
    }
}
