using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Runtime;

/// <summary>
/// The one tooltip box of the kit: a small panel in the theme's colours next to the mouse (or next to the
/// control the keyboard highlighted), shown after a short hover. Runs per frame only while a tooltip is
/// waiting or showing, and allocates nothing while it follows the pointer.
/// </summary>
internal sealed class TooltipLayer : MonoBehaviour
{
    private const float Delay = 0.45f;
    private const float MaxWidth = 460f;
    private static readonly Vector2 PointerOffset = new Vector2(22f, -26f);

    private RectTransform _root;
    private ThemeCapture _themes;
    private UiTheme _builtFor;
    private RectTransform _panel;
    private TMP_Text _text;
    private LayoutElement _width;
    private RectTransform _target;
    private bool _followPointer;
    private float _showAt;
    private bool _shown;

    public void Init(RectTransform root, ThemeCapture themes)
    {
        _root = root;
        _themes = themes;
        enabled = false;
    }

    /// <summary>The mouse is over a control with a tooltip.</summary>
    public void Hover(RectTransform target, string text) => Begin(target, text, pointer: true, Time.unscaledTime + Delay);

    /// <summary>The keyboard highlighted a control with a tooltip: show it beside the control.</summary>
    public void Focus(RectTransform target, string text) => Begin(target, text, pointer: false, Time.unscaledTime + Delay);

    /// <summary>Hides the tooltip if it belongs to <paramref name="target"/> (or any, when null).</summary>
    public void Leave(RectTransform target)
    {
        if (target != null && target != _target)
            return;
        _target = null;
        Hide();
    }

    private void Begin(RectTransform target, string text, bool pointer, float at)
    {
        if (string.IsNullOrEmpty(text) || target == null)
            return;
        Ensure();
        _target = target;
        _followPointer = pointer;
        _text.text = text;
        // Short tips get a box their size; long ones wrap at the maximum width. Measured once per tip.
        _width.preferredWidth = Mathf.Min(MaxWidth, _text.GetPreferredValues(text).x + 4f);
        _showAt = at;
        Hide();
        enabled = true;
    }

    private void Update()
    {
        if (_target == null || !_target.gameObject.activeInHierarchy)
        {
            _target = null;
            Hide();
            enabled = false;
            return;
        }
        if (!_shown)
        {
            if (Time.unscaledTime < _showAt)
                return;
            _shown = true;
            _panel.SetAsLastSibling();
            _panel.gameObject.SetActive(true);
        }
        Place();
    }

    private void Place()
    {
        Vector2 local;
        if (_followPointer)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, UnityEngine.Input.mousePosition, null, out local);
            local += PointerOffset;
        }
        else
        {
            var corner = _root.InverseTransformPoint(_target.TransformPoint(new Vector3(_target.rect.xMax, _target.rect.yMax, 0f)));
            local = new Vector2(corner.x - 40f, corner.y - 8f);
        }

        // Keep the box on screen: flip it to the other side of the pointer when it would run off.
        var size = _panel.rect.size;
        var half = _root.rect.size * 0.5f;
        if (local.x + size.x > half.x)
            local.x = Mathf.Max(-half.x, local.x - size.x - PointerOffset.x * 2f);
        if (local.y - size.y < -half.y)
            local.y = Mathf.Min(half.y, local.y + size.y - PointerOffset.y * 2f);
        _panel.anchoredPosition = local;
    }

    private void Hide()
    {
        _shown = false;
        if (_panel != null && _panel.gameObject.activeSelf)
            _panel.gameObject.SetActive(false);
        if (_target == null)
            enabled = false;
    }

    private void Ensure()
    {
        var theme = _themes.Current;
        if (_panel != null && _builtFor == theme)
            return;
        if (_panel != null)
            Destroy(_panel.gameObject);
        _builtFor = theme;

        var panel = UiParts.Image(_root, "Tooltip", null, theme.Panel);
        _panel = panel.rectTransform;
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0f, 1f);
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.PanelEdge;
        outline.effectDistance = new Vector2(2f, -2f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 10, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _text = UiParts.Text(_panel, theme, "", theme.TextSize * 0.85f, theme.Text, TextAlignmentOptions.TopLeft);
        _width = UiParts.Layout(_text);
        _panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_panel != null)
            Destroy(_panel.gameObject);
    }
}
