using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ObenUIKit.Windows;

/// <summary>
/// The title of a floating window: dragging it moves the window, clicking it brings the window to the
/// front. The window can't be dragged so far that its title leaves the screen.
/// </summary>
internal sealed class WindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
{
    private const float KeepVisible = 80f;

    private RectTransform _window;
    private RectTransform _canvas;
    private Action _raise;

    public void Init(RectTransform window, Action raise)
    {
        _window = window;
        _raise = raise;
        var canvas = GetComponentInParent<Canvas>();
        _canvas = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
    }

    public void OnPointerDown(PointerEventData data) => _raise?.Invoke();

    public void OnBeginDrag(PointerEventData data) => _raise?.Invoke();

    public void OnDrag(PointerEventData data)
    {
        if (_window == null || _canvas == null)
            return;
        // The drag delta is in screen pixels; the window lives in the scaled canvas.
        float scale = _canvas.localScale.x > 0f ? _canvas.localScale.x : 1f;
        var position = _window.anchoredPosition + data.delta / scale;

        var half = _canvas.rect.size * 0.5f;
        var size = _window.rect.size;
        position.x = Mathf.Clamp(position.x, -half.x - size.x * 0.5f + KeepVisible, half.x + size.x * 0.5f - KeepVisible);
        position.y = Mathf.Clamp(position.y, -half.y + KeepVisible - size.y * 0.5f, half.y - size.y * 0.5f);
        _window.anchoredPosition = position;
    }
}
