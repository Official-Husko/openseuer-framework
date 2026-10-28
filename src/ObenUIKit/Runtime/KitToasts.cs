using System.Collections.Generic;
using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Runtime;

/// <summary>
/// The kit's own small notices in the top right corner, for where the game has no notification
/// panel (the main menu). They fade out after a few seconds. The component only runs while a
/// notice is showing.
/// </summary>
internal sealed class KitToasts : MonoBehaviour
{
    private const float Lifetime = 4.5f;
    private const float FadeTime = 0.6f;
    private const int MaxShown = 4;

    private sealed class Notice
    {
        public GameObject Root;
        public CanvasGroup Group;
        public float Until;
    }

    private readonly List<Notice> _shown = new List<Notice>();
    private RectTransform _stack;

    internal void Init(RectTransform root)
    {
        _stack = UiParts.Child(root, "Notices");
        _stack.anchorMin = _stack.anchorMax = _stack.pivot = new Vector2(1f, 1f);
        _stack.anchoredPosition = new Vector2(-30f, -30f);
        _stack.sizeDelta = new Vector2(440f, 0f);
        var layout = _stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperRight;
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        enabled = false;
    }

    internal void Show(UiTheme theme, string title, string text)
    {
        while (_shown.Count >= MaxShown)
            Remove(0);

        var panel = UiParts.Image(_stack, "Notice", null, theme.Panel);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.PanelEdge;
        outline.effectDistance = new Vector2(2f, -2f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 12, 14);
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        UiParts.Text(panel.transform, theme, title, theme.TextSize * 0.8f, theme.TextMuted, TextAlignmentOptions.TopLeft);
        UiParts.Text(panel.transform, theme, text, theme.TextSize, theme.Text, TextAlignmentOptions.TopLeft);

        var group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        _shown.Add(new Notice { Root = panel.gameObject, Group = group, Until = Time.unscaledTime + Lifetime });
        _stack.SetAsLastSibling();
        enabled = true;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < _shown.Count; i++)
        {
            var notice = _shown[i];
            if (notice.Root == null || now >= notice.Until)
            {
                Remove(i--);
                continue;
            }
            notice.Group.alpha = Mathf.Clamp01((notice.Until - now) / FadeTime);
        }
        if (_shown.Count == 0)
            enabled = false;
    }

    private void Remove(int index)
    {
        if (_shown[index].Root != null)
            Destroy(_shown[index].Root);
        _shown.RemoveAt(index);
    }
}
