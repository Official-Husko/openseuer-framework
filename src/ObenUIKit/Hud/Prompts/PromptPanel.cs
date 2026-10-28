using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit;

/// <summary>
/// The kit's own prompt, for when the game's tutorial prompt is busy or missing: the text over a small key
/// box, centred a little lower than the game's so both can show at once. Stamped from the theme.
/// </summary>
internal sealed class PromptPanel
{
    private const float Below = -250f;

    private readonly RectTransform _parent;
    private GameObject _root;
    private UiTheme _theme;
    private TMP_Text _text;
    private TMP_Text _key;
    private GameObject _keyBox;

    public PromptPanel(RectTransform parent)
    {
        _parent = parent;
    }

    public bool Showing => _root != null && _root.activeSelf;

    public void Show(UiTheme theme, string text, string key)
    {
        if (_root == null || _theme != theme)
            Build(theme);
        _text.text = text;
        _key.text = key ?? "";
        _keyBox.SetActive(!string.IsNullOrEmpty(key));
        _root.SetActive(true);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)_root.transform);
    }

    public void Hide()
    {
        if (_root != null && _root.activeSelf)
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
        var root = UiParts.Child(_parent, "Prompt");
        _root = root.gameObject;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, Below);
        var layout = _root.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fit = _root.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _text = UiParts.Text(root, theme, "", theme.TextSize * 1.05f, theme.Text);
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        var shadow = _text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);

        // A key cap: a grey edge around a dark face, like the game's key boxes.
        var edge = UiParts.Image(root, "Key", null, theme.TextMuted);
        _keyBox = edge.gameObject;
        var edgeLayout = _keyBox.AddComponent<HorizontalLayoutGroup>();
        edgeLayout.padding = new RectOffset(3, 3, 3, 3);
        edgeLayout.childControlWidth = true;
        edgeLayout.childControlHeight = true;
        var size = UiParts.Layout(edge);
        size.minWidth = 46f;
        size.minHeight = 46f;
        var face = UiParts.Image(edge.transform, "Face", null, theme.Panel);
        var faceLayout = face.gameObject.AddComponent<HorizontalLayoutGroup>();
        faceLayout.padding = new RectOffset(10, 10, 4, 4);
        faceLayout.childAlignment = TextAnchor.MiddleCenter;
        faceLayout.childControlWidth = true;
        faceLayout.childControlHeight = true;
        _key = UiParts.Text(face.transform, theme, "", theme.TextSize, theme.Text);
        _key.textWrappingMode = TextWrappingModes.NoWrap;
        _root.SetActive(false);
    }
}
