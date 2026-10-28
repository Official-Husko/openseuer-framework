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

        var template = pause.resumeMenuButton;
        var parent = template.transform.parent;
        var clone = Object.Instantiate(template, parent, false);
        clone.name = ObjectName;
        Strip(clone);

        var quit = parent.Find("Quit to Menu");
        clone.transform.SetSiblingIndex(quit != null ? quit.GetSiblingIndex() : parent.childCount - 1);
        var text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = "Mods";

        var button = clone.GetComponent<UnityEngine.UI.Button>();
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener(() => ModMenu.Open());
        _entry = clone;
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
            Object.DestroyImmediate(relay);
    }

    private static void Detach()
    {
        if (_entry == null)
            return;
        // Destroy waits for the end of the frame; take it out of the menu right away.
        _entry.SetActive(false);
        _entry.transform.SetParent(null, false);
        Object.Destroy(_entry);
        _entry = null;
    }
}
