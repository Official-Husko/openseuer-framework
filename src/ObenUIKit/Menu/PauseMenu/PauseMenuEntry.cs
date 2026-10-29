using System;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>
/// Adds MODS to the pause menu, just above "Quit to Menu". It's a copy of the pause menu's own
/// Resume button made inside the level (so it dies with it), with the copy's Relay objects removed:
/// they would fire the original's outputs and carry its save ids. The page's grid layout places
/// it, and the pause menu's keyboard handler picks up every direct child button by itself.
/// </summary>
internal static class PauseMenuEntry
{
    public const string ObjectName = "Mods (ObenUIKit)";

    private static GameObject _entry;

    public static void Init(ObenMod kit)
    {
        GameEvents.WhenWorldReady(kit, _ => Attach());
        kit.Track(Disposable.Create(Detach));
    }

    private static void Attach()
    {
        var pause = PauseMenu.instance;
        if (pause == null || pause.resumeMenuButton == null || (_entry != null && _entry.scene == pause.gameObject.scene))
            return;
        Detach();

        var clone = CopyResume(pause, ObjectName, () => ModMenu.Open());
        var parent = clone.transform.parent;
        var quit = parent.Find("Quit to Menu");
        clone.transform.SetSiblingIndex(quit != null ? quit.GetSiblingIndex() : parent.childCount - 1);
        var text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = "Mods";
        _entry = clone;
    }

    /// <summary>A copy of the pause menu's Resume button beside it, stripped of what would act like the original, that runs <paramref name="onClick"/>.</summary>
    internal static GameObject CopyResume(PauseMenu pause, string name, Action onClick)
    {
        var template = pause.resumeMenuButton;
        var clone = UnityEngine.Object.Instantiate(template, template.transform.parent, false);
        clone.name = name;
        Strip(clone);
        var button = clone.GetComponent<UnityEngine.UI.Button>();
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener(() => onClick());
        return clone;
    }

    /// <summary>Greys a copied button out (or back in) the way the game's own buttons show it.</summary>
    internal static void SetInteractable(GameObject copy, bool interactable)
    {
        var button = copy.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
            button.interactable = interactable;
        var effect = copy.GetComponent<TextButtonColorEffectTMP>();
        if (effect == null)
            return;
        effect.interactable = interactable;
        effect.disableColorEffect = !interactable;
        var text = copy.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.color = interactable ? Native.ButtonDefault(effect) : effect.DisabledColor;
    }

    /// <summary>Takes a copy out of the menu at once and destroys it.</summary>
    internal static void Discard(GameObject copy)
    {
        // Destroy waits for the end of the frame; take it out of the menu right away.
        copy.SetActive(false);
        copy.transform.SetParent(null, false);
        UnityEngine.Object.Destroy(copy);
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

    private static void Detach()
    {
        if (_entry == null)
            return;
        Discard(_entry);
        _entry = null;
    }
}
