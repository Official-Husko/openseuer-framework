using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Theme;

/// <summary>Cached accessors for the private fields of the game's UI components the kit reads.</summary>
internal static class Native
{
    public static readonly AccessTools.FieldRef<TextButtonColorEffectTMP, Color> ButtonDefault = Ref<TextButtonColorEffectTMP, Color>("defaultColor");
    public static readonly AccessTools.FieldRef<TextButtonColorEffectTMP, Color> ButtonHover = Ref<TextButtonColorEffectTMP, Color>("hoverColor");
    public static readonly AccessTools.FieldRef<TextButtonColorEffectTMP, Color> ButtonPress = Ref<TextButtonColorEffectTMP, Color>("pressColor");
    public static readonly AccessTools.FieldRef<TextButtonColorEffectTMP, Color> ButtonDisabled = Ref<TextButtonColorEffectTMP, Color>("disabledColor");
    public static readonly AccessTools.FieldRef<TextButtonColorEffectTMP, Relay> ButtonOnSelected = Ref<TextButtonColorEffectTMP, Relay>("onSelected");
    public static readonly AccessTools.FieldRef<TextButtonColorEffectTMP, Relay> ButtonOnUnselected = Ref<TextButtonColorEffectTMP, Relay>("onUnselected");
    public static readonly AccessTools.FieldRef<TextButtonColorEffectTMP, Color> ButtonDefaultColor = Ref<TextButtonColorEffectTMP, Color>("defaultColor");

    public static readonly AccessTools.FieldRef<OptionsButton, Image> RowImage = Ref<OptionsButton, Image>("image");
    public static readonly AccessTools.FieldRef<OptionsButton, Color> RowDefault = Ref<OptionsButton, Color>("defaultColor");
    public static readonly AccessTools.FieldRef<OptionsButton, Color> RowHover = Ref<OptionsButton, Color>("hoverColor");

    public static readonly AccessTools.FieldRef<MainMenuNavigationHelper, UnityEngine.UI.Button> MenuOptions = Ref<MainMenuNavigationHelper, UnityEngine.UI.Button>("options");
    public static readonly AccessTools.FieldRef<MainMenuNavigationHelper, UnityEngine.UI.Button> MenuUnknown = Ref<MainMenuNavigationHelper, UnityEngine.UI.Button>("unknown");
    public static readonly AccessTools.FieldRef<MainMenuNavigationHelper, UnityEngine.UI.Button> MenuNewGame = Ref<MainMenuNavigationHelper, UnityEngine.UI.Button>("newGame");
    public static readonly AccessTools.FieldRef<MainMenuNavigationHelper, UnityEngine.UI.Button> MenuLoadGame = Ref<MainMenuNavigationHelper, UnityEngine.UI.Button>("loadGame");
    public static readonly AccessTools.FieldRef<MainMenuNavigationHelper, UnityEngine.UI.Button> MenuCredits = Ref<MainMenuNavigationHelper, UnityEngine.UI.Button>("credits");
    public static readonly AccessTools.FieldRef<MainMenuNavigationHelper, UnityEngine.UI.Button> MenuQuit = Ref<MainMenuNavigationHelper, UnityEngine.UI.Button>("quit");

    public static readonly AccessTools.FieldRef<OptionsMenu, Slider> OptionsFov = Ref<OptionsMenu, Slider>("fovSlider");
    public static readonly AccessTools.FieldRef<OptionsMenu, OptionsButton> OptionsLanguage = Ref<OptionsMenu, OptionsButton>("languageButton");

    private static AccessTools.FieldRef<TObject, TField> Ref<TObject, TField>(string name) where TObject : class =>
        AccessTools.FieldRefAccess<TObject, TField>(name);
}
