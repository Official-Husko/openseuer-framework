using ObenUIKit.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>The dark red bar the game shows behind the option row under the cursor.</summary>
internal sealed class KitRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image _background;
    private UiTheme _theme;

    internal void Init(Image background, UiTheme theme)
    {
        _background = background;
        _theme = theme;
        Paint(false);
    }

    public void OnPointerEnter(PointerEventData data) => Paint(true);

    public void OnPointerExit(PointerEventData data) => Paint(false);

    private void Paint(bool hover)
    {
        if (_background != null)
            _background.color = hover ? _theme.RowHover : Color.clear;
    }
}
