using System;
using ObenUIKit.Input;
using ObenUIKit.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>
/// The dark red bar the game shows behind the option row under the cursor. It's also what lights
/// up when the keyboard or controller highlights the row, and it carries the row's navigation
/// actions: what left and right do, and what Enter or a click does.
/// </summary>
internal sealed class KitRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, INavTarget
{
    private Image _background;
    private UiTheme _theme;
    private UiNav _nav;
    private Func<int, bool> _step;
    private Action _activate;
    private Action _click;
    private Func<bool> _reset;
    private bool _hover;
    private bool _focus;

    public RectTransform Rect => (RectTransform)transform;

    public bool Focusable => isActiveAndEnabled;

    internal void Init(Image background, UiTheme theme)
    {
        _background = background;
        _theme = theme;
        Paint();
    }

    /// <summary>Makes the row reachable from the keyboard. <paramref name="click"/> also runs when the row is clicked.</summary>
    internal void Bind(UiNav nav, Func<int, bool> step, Action activate, Action click = null)
    {
        _nav = nav;
        _step = step;
        _activate = activate;
        _click = click;
    }

    public void SetFocus(bool on)
    {
        _focus = on;
        Paint();
    }

    public void Activate() => _activate?.Invoke();

    public bool Step(int delta) => _step != null && _step(delta);

    /// <summary>What the reset key does on this row (a setting going back to its default). True when it did something.</summary>
    internal void SetReset(Func<bool> reset) => _reset = reset;

    public bool Reset() => _reset != null && _reset();

    public void OnPointerEnter(PointerEventData data)
    {
        _hover = true;
        _nav?.Hover(this);
        Paint();
    }

    public void OnPointerExit(PointerEventData data)
    {
        _hover = false;
        _nav?.Unhover(this);
        Paint();
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left)
            _click?.Invoke();
    }

    private void Paint()
    {
        if (_background != null)
            _background.color = _hover || _focus ? _theme.RowHover : Color.clear;
    }
}
