using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>
/// "ObenseuerExtended v0.1.0" just above the game's own version at the bottom of the pause menu, in the same small
/// text. It is a new label styled after the game's (whose ShowVersion script would overwrite a copy), made in every
/// level and dying with it.
/// </summary>
internal static class PauseMenuVersion
{
    private const string ObjectName = "ObenseuerExtended version (ObenUIKit)";

    private static GameObject _line;

    public static void Init(ObenMod kit)
    {
        GameEvents.WhenWorldReady(kit, _ => Attach());
        kit.Track(Disposable.Create(Detach));
    }

    private static void Attach()
    {
        var pause = PauseMenu.instance;
        if (pause == null || (_line != null && _line.scene == pause.gameObject.scene))
            return;
        Detach();
        TMP_Text game = null;
        foreach (var text in pause.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponent<ShowVersion>() != null)
            {
                game = text;
                break;
            }
        }
        if (game == null)
            return;

        var line = new GameObject(ObjectName, typeof(RectTransform));
        line.transform.SetParent(game.transform.parent, false);
        line.transform.SetSiblingIndex(game.transform.GetSiblingIndex());
        var rect = (RectTransform)line.transform;
        var source = game.rectTransform;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = new Vector2(Mathf.Max(source.sizeDelta.x, 400f), source.sizeDelta.y);
        rect.anchoredPosition = source.anchoredPosition + new Vector2(0f, source.sizeDelta.y);

        var label = line.AddComponent<TextMeshProUGUI>();
        label.font = game.font;
        label.fontSharedMaterial = game.fontSharedMaterial;
        label.fontSize = game.fontSize;
        label.fontStyle = game.fontStyle;
        label.color = game.color;
        label.alignment = game.alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = "ObenseuerExtended v" + Loader.Version;
        _line = line;
    }

    private static void Detach()
    {
        if (_line == null)
            return;
        Object.Destroy(_line);
        _line = null;
    }
}
