using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.NewGameScreen;

/// <summary>
/// The parts of the New Game screen's difficulty page the option rows work with, bound once: the settings list
/// (<c>Scroll Settings</c>, shown by the game for Custom only) whose content the page's pad navigation walks,
/// the description block shown for other levels (<c>Scroll Info</c>), and the description box beside the list.
/// </summary>
internal sealed class NewGamePage
{
    private static readonly AccessTools.FieldRef<MainMenuNavigationHandler, MainCharacterMenuDifficultyNavigationHelper> HandlerHelper =
        AccessTools.FieldRefAccess<MainMenuNavigationHandler, MainCharacterMenuDifficultyNavigationHelper>("characterMenuDifficultyNavigationHelper");
    private static readonly AccessTools.FieldRef<MainCharacterMenuDifficultyNavigationHelper, GameObject> ButtonsPanel =
        AccessTools.FieldRefAccess<MainCharacterMenuDifficultyNavigationHelper, GameObject>("buttonsPanel");
    private static readonly AccessTools.FieldRef<MainCharacterMenuDifficultyNavigationHelper, ScrollRect> ScrollOf =
        AccessTools.FieldRefAccess<MainCharacterMenuDifficultyNavigationHelper, ScrollRect>("scrollRect");
    private static readonly AccessTools.FieldRef<DifficultyUI, GameObject> SettingsBlock = AccessTools.FieldRefAccess<DifficultyUI, GameObject>("difficultySettings");
    private static readonly AccessTools.FieldRef<DifficultyUI, GameObject> InfoBlock = AccessTools.FieldRefAccess<DifficultyUI, GameObject>("difficultyInfo");
    private static readonly AccessTools.FieldRef<DifficultyUI, TextMeshProUGUI> InfoTextOf = AccessTools.FieldRefAccess<DifficultyUI, TextMeshProUGUI>("difficultyInfoText");
    private static readonly AccessTools.FieldRef<DifficultyUI, GameObject> DescriptionPanel = AccessTools.FieldRefAccess<DifficultyUI, GameObject>("settingDescriptionPanel");
    private static readonly AccessTools.FieldRef<DifficultyUI, TextMeshProUGUI> DescriptionTitle = AccessTools.FieldRefAccess<DifficultyUI, TextMeshProUGUI>("settingDescriptionTitle");
    private static readonly AccessTools.FieldRef<DifficultyUI, TextMeshProUGUI> DescriptionText = AccessTools.FieldRefAccess<DifficultyUI, TextMeshProUGUI>("settingDescriptionText");

    private NewGamePage() { }

    public DifficultyUI Ui { get; private set; }

    public MainCharacterMenuDifficultyNavigationHelper Helper { get; private set; }

    /// <summary>The settings list's content: the game's setting rows, laid out by a vertical layout group.</summary>
    public Transform Panel { get; private set; }

    public GameObject Settings { get; private set; }

    public GameObject Info { get; private set; }

    public TextMeshProUGUI InfoText { get; private set; }

    public ScrollRect Scroll { get; private set; }

    /// <summary>False once the menu scene that held the page is gone.</summary>
    public bool Alive => Ui != null && Panel != null;

    /// <summary>The page around <paramref name="ui"/>, or null with the reason when a part isn't where 0.5.13 has it.</summary>
    public static NewGamePage Find(DifficultyUI ui, out string missing)
    {
        missing = null;
        var handler = MainMenuNavigationHandler.Instance;
        var helper = handler != null ? HandlerHelper(handler) : null;
        if (helper == null)
            helper = ui.GetComponentInParent<MainCharacterMenuDifficultyNavigationHelper>(true);
        var panel = helper != null ? ButtonsPanel(helper) : null;
        var page = new NewGamePage
        {
            Ui = ui,
            Helper = helper,
            Panel = panel != null ? panel.transform : null,
            Settings = SettingsBlock(ui),
            Info = InfoBlock(ui),
            InfoText = InfoTextOf(ui),
            Scroll = helper != null ? ScrollOf(helper) : null,
        };
        if (helper == null)
            missing = "the page's navigation helper";
        else if (page.Panel == null || page.Settings == null || !page.Panel.IsChildOf(page.Settings.transform))
            missing = "the settings list";
        else if (page.Info == null || page.InfoText == null)
            missing = "the difficulty description";
        return missing == null ? page : null;
    }

    /// <summary>
    /// A game setting row to copy: a row with its own <c>OptionsButton</c> and setting description, holding one
    /// arrow button with a value text. Null when the list has none of that shape.
    /// </summary>
    public GameObject Template()
    {
        foreach (Transform child in Panel)
        {
            if (NewGameRow.IsOwn(child.gameObject) || child.GetComponent<DifficultySettingDescriptionUI>() == null)
                continue;
            if (NewGameRow.Cycle(child.gameObject) != null && NewGameRow.TitleOf(child.gameObject) != null)
                return child.gameObject;
        }
        return null;
    }

    public void ShowDescription(string title, string text)
    {
        var titleText = DescriptionTitle(Ui);
        var bodyText = DescriptionText(Ui);
        var panel = DescriptionPanel(Ui);
        if (titleText != null)
            titleText.text = title;
        if (bodyText != null)
            bodyText.text = text;
        if (panel != null)
            panel.SetActive(true);
    }

    public void HideDescription() => Ui.HideSettingDescription();

    /// <summary>Has the page's pad navigation walk the rows that are showing now.</summary>
    public void RefreshNavigation() => Helper.UpdateList();
}
