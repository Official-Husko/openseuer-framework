using System;
using ObenUIKit.Controls;
using ObenUIKit.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Preview;

/// <summary>
/// "Body" and "Face" in the bottom-right corner of a model preview, the picked one lit: the switch a mod shows
/// with <see cref="ModelPreviewHandle.ShowFramingSwitch"/>. Mouse only; the keyboard goes through the handle.
/// </summary>
internal sealed class FramingSwitch
{
    private static readonly string[] Names = { "Body", "Face" };

    private readonly GameObject _root;
    private readonly KitButton[] _buttons = new KitButton[Names.Length];

    private FramingSwitch(GameObject root)
    {
        _root = root;
    }

    public static FramingSwitch Build(RectTransform frame, KitContext kit, Action<PreviewFraming> picked)
    {
        var strip = UiParts.Child(frame, "Framing");
        strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(1f, 0f);
        strip.anchoredPosition = new Vector2(-12f, 8f);
        var layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        strip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var made = new FramingSwitch(strip.gameObject);
        for (int i = 0; i < Names.Length; i++)
        {
            var holder = UiParts.Child(strip, Names[i]);
            var label = UiParts.Text(holder, kit.Theme, Names[i], kit.Theme.TextSize * 0.85f, kit.Theme.Text);
            label.fontStyle = FontStyles.Underline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            UiParts.Stretch(label.rectTransform);
            UiParts.Width(holder, label.preferredWidth + 8f);
            UiParts.Height(holder, 36f);
            holder.gameObject.AddComponent<Image>().color = Color.clear;
            var framing = (PreviewFraming)i;
            made._buttons[i] = holder.gameObject.AddComponent<KitButton>();
            made._buttons[i].Init(label, kit, () => picked(framing));
        }
        strip.gameObject.SetActive(false);
        return made;
    }

    public bool Visible
    {
        get => _root != null && _root.activeSelf;
        set
        {
            if (_root != null)
                _root.SetActive(value);
        }
    }

    public void Show(PreviewFraming framing)
    {
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null)
                continue;
            _buttons[i].Selected = i == (int)framing;
            _buttons[i].Paint();
        }
    }
}
