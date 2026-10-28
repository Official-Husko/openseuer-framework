using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Input;

/// <summary>Scrolls a window's body so a control is inside the visible part.</summary>
internal static class NavScroll
{
    // Controls with a title bar above them show it too.
    private const float PadTop = 56f;
    private const float PadBottom = 10f;

    private static readonly Vector3[] Corners = new Vector3[4];

    public static void Reveal(ScrollRect scroll, RectTransform target)
    {
        var content = scroll.content;
        var viewport = scroll.viewport;
        if (content == null || viewport == null || target == null)
            return;

        // The content's pivot is its top edge, so local y is 0 at the top and negative below it.
        target.GetWorldCorners(Corners);
        float top = content.InverseTransformPoint(Corners[1]).y + PadTop;
        float bottom = content.InverseTransformPoint(Corners[0]).y - PadBottom;

        float view = viewport.rect.height;
        float offset = content.anchoredPosition.y;
        if (top > -offset)
            offset = -top;
        else if (bottom < -offset - view)
            offset = -bottom - view;

        offset = Mathf.Clamp(offset, 0f, Mathf.Max(0f, content.rect.height - view));
        if (!Mathf.Approximately(offset, content.anchoredPosition.y))
        {
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
            scroll.velocity = Vector2.zero;
        }
    }
}
