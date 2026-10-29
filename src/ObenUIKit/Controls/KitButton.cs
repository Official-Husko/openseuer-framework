using System;
using ObenLoader.Logging;
using ObenUIKit.Input;
using ObenUIKit.Runtime;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>
/// A text button that behaves like the game's own menu buttons: text colour for normal, hover,
/// pressed and disabled taken from the game, its hover and click sounds, click on release.
/// </summary>
internal sealed class KitButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler, IPointerClickHandler, INavTarget
{
    private TMP_Text _text;
    private UiTheme _theme;
    private UiSounds _sounds;
    private ModLogger _log;
    private Action _onClick;
    private UiNav _nav;
    private bool _hover;
    private bool _focus;
    private bool _pressed;
    private bool _interactable = true;
    private bool _quiet;
    private Image _back;

    public bool Interactable
    {
        get => _interactable;
        set
        {
            _interactable = value;
            Paint();
        }
    }

    /// <summary>Kept highlighted, for the selected tab or page.</summary>
    public bool Selected { get; set; }

    public RectTransform Rect => (RectTransform)transform;

    public bool Focusable => _interactable && isActiveAndEnabled;

    /// <summary>Makes the button reachable from the keyboard and lets the mouse move the highlight.</summary>
    internal void Bind(UiNav nav) => _nav = nav;

    public void SetFocus(bool on)
    {
        _focus = on;
        Paint();
    }

    public void Activate() => Click();

    public bool Step(int delta) => false;

    /// <summary>
    /// The look of a styled button: <paramref name="quiet"/> rests in the muted color, <paramref name="back"/> is a
    /// box behind the text that lights up like the game's option rows.
    /// </summary>
    internal void Style(bool quiet, Image back)
    {
        _quiet = quiet;
        _back = back;
        Paint();
    }

    internal void Init(TMP_Text text, KitContext kit, Action onClick)
    {
        _text = text;
        _theme = kit.Theme;
        _sounds = kit.Sounds;
        _log = kit.Log;
        _onClick = onClick;
        Paint();
    }

    public void OnPointerEnter(PointerEventData data)
    {
        _hover = true;
        _nav?.Hover(this);
        if (_interactable)
            _sounds.Hover();
        Paint();
    }

    public void OnPointerExit(PointerEventData data)
    {
        _hover = false;
        _pressed = false;
        _nav?.Unhover(this);
        Paint();
    }

    public void OnPointerDown(PointerEventData data)
    {
        _pressed = true;
        Paint();
    }

    public void OnPointerUp(PointerEventData data)
    {
        _pressed = false;
        Paint();
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (!_interactable || data.button != PointerEventData.InputButton.Left)
            return;
        Click();
    }

    private void Click()
    {
        _sounds.Press();
        try { _onClick?.Invoke(); }
        catch (Exception e) { _log.Error($"Button '{_text.text}' failed", e); }
    }

    internal void Paint()
    {
        if (_text == null)
            return;
        bool lit = _hover || _focus || Selected;
        _text.color = !_interactable ? _theme.TextDisabled
            : _pressed ? _theme.TextPress
            : lit && _back == null ? _theme.TextHover
            : _quiet ? _theme.TextMuted
            : _theme.Text;
        if (_back != null)
            _back.color = _interactable && (lit || _pressed) ? _theme.RowHover : _theme.Header;
    }
}
