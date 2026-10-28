using System;
using ObenUIKit.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ObenUIKit.Controls;

/// <summary>
/// A plain clickable area with the game's hover and click sounds, for controls that aren't text (colour
/// swatches, list rows). <see cref="Hovered"/> lets the control repaint itself.
/// </summary>
internal sealed class KitClick : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private UiSounds _sounds;
    private Action _onClick;

    /// <summary>The pointer came in (true) or left (false).</summary>
    public Action<bool> Hovered;

    internal void Init(KitContext kit, Action onClick)
    {
        _sounds = kit.Sounds;
        _onClick = onClick;
    }

    public void OnPointerEnter(PointerEventData data)
    {
        _sounds?.Hover();
        Hovered?.Invoke(true);
    }

    public void OnPointerExit(PointerEventData data) => Hovered?.Invoke(false);

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left)
            return;
        _sounds?.Press();
        _onClick?.Invoke();
    }
}
