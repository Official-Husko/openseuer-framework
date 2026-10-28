using ObenUIKit.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ObenUIKit.Controls;

/// <summary>Carries a control's tooltip text and tells the kit's tooltip box when the mouse comes and goes.</summary>
internal sealed class KitTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TooltipLayer _layer;

    public string Text { get; set; }

    internal void Init(TooltipLayer layer, string text)
    {
        _layer = layer;
        Text = text;
    }

    public void OnPointerEnter(PointerEventData data)
    {
        if (_layer != null)
            _layer.Hover((RectTransform)transform, Text);
    }

    public void OnPointerExit(PointerEventData data) => Leave();

    private void OnDisable() => Leave();

    internal void Focus()
    {
        if (_layer != null)
            _layer.Focus((RectTransform)transform, Text);
    }

    internal void Leave()
    {
        if (_layer != null)
            _layer.Leave((RectTransform)transform);
    }
}
