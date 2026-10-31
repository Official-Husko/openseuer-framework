using System;
using ObenFramework.NewGames;
using ObenLoader.Mods;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace ObenUIKit.NewGameScreen;

/// <summary>
/// One new game option on the difficulty page: a copy of one of the game's setting rows (its title on the
/// left, the value between two arrows on the right) whose arrows step the option instead of a game setting.
/// The copy keeps the row's look, sounds and hover effects; the arrows' calls into the page are switched off
/// and the game's setting description is replaced by the option's own.
/// </summary>
internal sealed class NewGameRow
{
    public const string Prefix = "ObenUIKit option ";

    private readonly NewGamePage _page;
    private TMP_Text _value;

    private NewGameRow(NewGamePage page, NewGameOption option)
    {
        _page = page;
        Option = option;
    }

    public NewGameOption Option { get; }

    public GameObject Root { get; private set; }

    public static bool IsOwn(GameObject go) => go.name.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>The row's arrow button: the <c>OptionsButton</c> below the row with both arrows and a value text.</summary>
    public static OptionsButton Cycle(GameObject row)
    {
        foreach (var button in row.GetComponentsInChildren<OptionsButton>(true))
        {
            if (button.gameObject != row && button.buttonLeft != null && button.buttonRight != null && button.text != null && button.slider == null)
                return button;
        }
        return null;
    }

    /// <summary>The row's title: the first text that isn't part of the arrow button.</summary>
    public static TMP_Text TitleOf(GameObject row)
    {
        var cycle = Cycle(row);
        foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (cycle == null || !text.transform.IsChildOf(cycle.transform))
                return text;
        }
        return null;
    }

    public static NewGameRow Create(NewGamePage page, GameObject template, NewGameOption option)
    {
        var row = new NewGameRow(page, option);
        var go = UnityEngine.Object.Instantiate(template, page.Panel, false);
        go.name = Prefix + option.ModId + "." + option.Key;
        row.Root = go;
        try
        {
            row.Wire(go);
        }
        catch
        {
            UnityEngine.Object.Destroy(go);
            throw;
        }
        return row;
    }

    private void Wire(GameObject go)
    {
        // The game's description names one of its own settings; this row describes the option instead.
        var description = go.GetComponent<DifficultySettingDescriptionUI>();
        if (description != null)
            UnityEngine.Object.DestroyImmediate(description);

        var cycle = Cycle(go);
        _value = cycle.text;
        TitleOf(go).text = Option.Title;
        Mute(cycle.buttonLeft.onClick);
        Mute(cycle.buttonRight.onClick);
        cycle.buttonLeft.onClick.AddListener(() => Move(false));
        cycle.buttonRight.onClick.AddListener(() => Move(true));
        cycle.Enable();

        var outer = go.GetComponent<OptionsButton>();
        if (outer != null)
            outer.SelectionChanged += Selected;
        Render();
    }

    /// <summary>Shows the value the screen holds for the option now.</summary>
    public void Render()
    {
        if (_value != null)
            _value.text = Option.Display(Current());
    }

    private string Current() => NewGameOptions.ScreenValue(Option.ModId, Option.Key) ?? Option.Default;

    private void Move(bool right)
    {
        try
        {
            // The option may have gone with its mod; the list is rebuilt right after that.
            if (NewGameOptions.Find(Option.ModId, Option.Key) != Option)
                return;
            NewGameOptions.Choose(Option.ModId, Option.Key, NewGameOptions.ScreenStep(Option, Current(), right));
            Render();
        }
        catch (Exception e)
        {
            UIKitMod.Instance?.Log.Error($"Changing the new game option {Option.ModId}.{Option.Key} failed", e);
        }
    }

    private void Selected(bool selected)
    {
        if (!_page.Alive)
            return;
        if (!selected)
        {
            _page.HideDescription();
            return;
        }
        var info = ModRegistry.Find(Option.ModId);
        string from = "From " + (info != null ? info.Manifest.DisplayName : Option.ModId) + ".";
        _page.ShowDescription(Option.Title, Option.Description.Length > 0 ? Option.Description + "\n\n" + from : from);
    }

    // Keeps the click sound, drops the call into the page's own setting.
    private static void Mute(UnityEvent onClick)
    {
        for (int i = 0; i < onClick.GetPersistentEventCount(); i++)
        {
            if (onClick.GetPersistentTarget(i) is DifficultyUI)
                onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
        onClick.RemoveAllListeners();
    }
}
