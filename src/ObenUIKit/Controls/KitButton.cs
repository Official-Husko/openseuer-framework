using System;
using ObenLoader.Logging;
using ObenUIKit.Runtime;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ObenUIKit.Controls;

/// <summary>
/// A text button that behaves like the game's own menu buttons: text colour for normal, hover,
/// pressed and disabled taken from the game, its hover and click sounds, click on release.
/// </summary>
internal sealed class KitButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
    IPointerUpHandler, IPointerClickHandler
{
    private TMP_Text _text;
    private UiTheme _theme;
    private UiSounds _sounds;
    private ModLogger _log;
    private Action _onClick;
    private bool _hover;
    private bool _pressed;
    private bool _interactable = true;

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
        if (_interactable)
            _sounds.Hover();
        Paint();
    }

    public void OnPointerExit(PointerEventData data)
    {
        _hover = false;
        _pressed = false;
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
        _sounds.Press();
        try { _onClick?.Invoke(); }
        catch (Exception e) { _log.Error($"Button '{_text.text}' failed", e); }
    }

    internal void Paint()
    {
        if (_text == null)
            return;
        _text.color = !_interactable ? _theme.TextDisabled
            : _pressed ? _theme.TextPress
            : _hover || Selected ? _theme.TextHover
            : _theme.Text;
    }
}
