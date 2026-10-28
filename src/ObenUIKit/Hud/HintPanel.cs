using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit;

/// <summary>
/// The kit's own hint panel, stamped from the theme, for where the game's hint overlay doesn't exist
/// (the main menu). A box on the right edge with a small title and the text, like the game's hints.
/// </summary>
internal sealed class HintPanel
{
    private const float Width = 440f;

    private readonly RectTransform _parent;
    private GameObject _root;
    private UiTheme _theme;
    private TMP_Text _title;
    private TMP_Text _body;

    public HintPanel(RectTransform parent)
    {
        _parent = parent;
    }

    public void Show(UiTheme theme, string title, string text)
    {
        if (_root == null || _theme != theme)
            Build(theme);
        _title.text = title;
        _body.text = text;
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)_root.transform);
    }

    public void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
            _root.SetActive(visible);
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    public void Dispose()
    {
        if (_root != null)
            Object.Destroy(_root);
        _root = null;
    }

    private void Build(UiTheme theme)
    {
        Dispose();
        _theme = theme;
        var panel = UiParts.Image(_parent, "Hint", null, theme.Panel);
        _root = panel.gameObject;
        var rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-30f, 0f);
        rect.sizeDelta = new Vector2(Width, 0f);

        var outline = _root.AddComponent<Outline>();
        outline.effectColor = theme.PanelEdge;
        outline.effectDistance = new Vector2(2f, -2f);
        var layout = _root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 14, 16);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _title = UiParts.Text(rect, theme, "", theme.TextSize * 0.85f, theme.TextMuted, TextAlignmentOptions.TopLeft);
        _body = UiParts.Text(rect, theme, "", theme.TextSize, theme.Text, TextAlignmentOptions.TopLeft);
        _root.SetActive(false);
    }
}
