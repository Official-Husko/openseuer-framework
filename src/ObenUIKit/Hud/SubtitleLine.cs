using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit;

/// <summary>
/// One line of text near the bottom of the screen, in the game's font on a dark strip, that fades out
/// when its time is up. A new line replaces the one showing. The clock waits while the line is hidden.
/// </summary>
internal sealed class SubtitleLine
{
    private const float FadeTime = 0.5f;
    private const float Width = 1300f;

    private readonly RectTransform _parent;
    private GameObject _root;
    private CanvasGroup _group;
    private TMP_Text _text;
    private UiTheme _theme;
    private float _remaining;
    private bool _hidden;

    public SubtitleLine(RectTransform parent)
    {
        _parent = parent;
    }

    public bool Active => _remaining > 0f;

    public void Show(UiTheme theme, string text, float seconds, bool hidden)
    {
        if (_root == null || _theme != theme)
            Build(theme);
        _text.text = text;
        _remaining = seconds;
        _hidden = hidden;
        _group.alpha = 1f;
        Apply();
    }

    /// <summary>Counts the time down and fades the line near the end.</summary>
    public void Tick(float delta, bool hidden)
    {
        if (_root == null)
            return;
        if (hidden != _hidden)
        {
            _hidden = hidden;
            Apply();
        }
        if (hidden || _remaining <= 0f)
            return;

        _remaining -= delta;
        if (_remaining <= 0f)
        {
            _remaining = 0f;
            Apply();
            return;
        }
        _group.alpha = Mathf.Clamp01(_remaining / FadeTime);
    }

    public void Dispose()
    {
        if (_root != null)
            Object.Destroy(_root);
        _root = null;
        _remaining = 0f;
    }

    private void Apply()
    {
        bool visible = _remaining > 0f && !_hidden;
        if (_root.activeSelf != visible)
            _root.SetActive(visible);
    }

    private void Build(UiTheme theme)
    {
        Dispose();
        _theme = theme;
        var strip = UiParts.Image(_parent, "Subtitle", null, theme.Dim);
        _root = strip.gameObject;
        var rect = strip.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 110f);
        rect.sizeDelta = new Vector2(Width, 0f);

        var layout = _root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 8, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _group = _root.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _text = UiParts.Text(rect, theme, "", theme.TextSize * 1.2f, theme.Text, TextAlignmentOptions.Center);
        _hidden = false;
        _root.SetActive(false);
    }
}
