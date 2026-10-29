using System;
using System.Reflection;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>
/// "ObenseuerExtended v0.1.0 (57a4977)" just above the game's own version at the bottom of the pause menu, in the same
/// small text. It is a new label styled after the game's (whose ShowVersion script would overwrite a copy), made in
/// every level and dying with it.
/// </summary>
internal static class PauseMenuVersion
{
    private const string ObjectName = "ObenseuerExtended version (ObenUIKit)";

    /// <summary>
    /// The kit's own stamp as "0.1.0 (57a4977)": the version and the commit it was built from ("-dirty" with
    /// uncommitted changes), as an experimental release is titled. Every assembly of a build carries the same one and
    /// a hot reload brings the new kit's, so the line tells which build is running.
    /// </summary>
    public static readonly string Version = Stamp();

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
        label.text = "ObenseuerExtended v" + Version;
        _line = line;
    }

    private static string Stamp()
    {
        var stamp = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
            typeof(PauseMenuVersion).Assembly, typeof(AssemblyInformationalVersionAttribute));
        string text = stamp?.InformationalVersion;
        if (string.IsNullOrEmpty(text))
            return Loader.Version;
        int plus = text.IndexOf('+');
        return plus > 0 ? text.Substring(0, plus) + " (" + text.Substring(plus + 1) + ")" : text;
    }

    private static void Detach()
    {
        if (_line == null)
            return;
        UnityEngine.Object.Destroy(_line);
        _line = null;
    }
}
