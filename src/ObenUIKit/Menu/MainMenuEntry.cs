using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>
/// Turns the main menu's unused "???" button into MODS. The button already sits in the menu's
/// 2x3 grid with the game's hover sound and colours, so keyboard and controller navigation work
/// without touching the game's navigation code. Put back as it was when the kit unloads.
/// </summary>
internal static class MainMenuEntry
{
    public const string Label = "Mods";

    private static UnityEngine.UI.Button _button;
    private static TMP_Text _text;
    private static string _originalText;
    private static bool _originalInteractable;

    public static void Init(ObenMod kit)
    {
        GameEvents.MainMenu.Subscribe(kit, () => kit.StartCoroutine(AttachNextFrame()));
        kit.Track(Disposable.Create(Detach));
        if (WorldState.InMainMenu)
            Attach();
    }

    private static System.Collections.IEnumerator AttachNextFrame()
    {
        yield return null;
        Attach();
    }

    private static void Attach()
    {
        Detach();
        var helper = FindHelper();
        var button = helper != null ? Native.MenuUnknown(helper) : null;
        if (button == null)
            return;

        _button = button;
        _text = button.GetComponentInChildren<TMP_Text>(true);
        _originalText = _text != null ? _text.text : null;
        _originalInteractable = button.interactable;

        if (_text != null)
            _text.text = Label;
        SetInteractable(true);
        button.onClick.AddListener(Open);
    }

    private static void Detach()
    {
        if (_button == null)
            return;
        _button.onClick.RemoveListener(Open);
        SetInteractable(_originalInteractable);
        if (_text != null && _originalText != null)
            _text.text = _originalText;
        _button = null;
        _text = null;
    }

    private static void SetInteractable(bool value)
    {
        _button.interactable = value;
        var effect = _button.GetComponent<TextButtonColorEffectTMP>();
        if (effect != null)
            effect.interactable = value;
    }

    private static void Open() => ModMenu.Open();

    private static MainMenuNavigationHelper FindHelper()
    {
        foreach (var helper in Resources.FindObjectsOfTypeAll<MainMenuNavigationHelper>())
        {
            if (helper.gameObject.scene.IsValid())
                return helper;
        }
        return null;
    }
}
