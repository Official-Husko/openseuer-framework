using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Input;

/// <summary>Scrolls a window's body so a control is inside the visible part, or a step toward it.</summary>
internal static class NavScroll
{
    // Controls with a title bar above them show it too.
    private const float PadTop = 56f;
    private const float PadBottom = 10f;
    private const float MinStep = 80f;
    private const float StepShare = 0.4f;

    private static readonly Vector3[] Corners = new Vector3[4];

    /// <summary>
    /// Scrolls every scrolling area around <paramref name="target"/>, innermost first (a scroll area inside the
    /// body, a full window's side panel, the body), so the target shows.
    /// </summary>
    public static void RevealAll(RectTransform target)
    {
        var parent = target != null ? target.parent : null;
        while (parent != null)
        {
            var scroll = parent.GetComponentInParent<ScrollRect>();
            if (scroll == null)
                return;
            Reveal(scroll, target);
            parent = scroll.transform.parent;
        }
    }

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

    /// <summary>
    /// Scrolls the body one step in <paramref name="direction"/> while <paramref name="next"/> (the control the
    /// arrows would go to next, or null when that one isn't in the body) is still out of view, so the text
    /// between controls can be read with the keyboard or pad. False when the next control is in view already
    /// or the body can't scroll that way; the focus moves then.
    /// </summary>
    public static bool StepToward(ScrollRect scroll, RectTransform next, int direction)
    {
        var content = scroll.content;
        var viewport = scroll.viewport;
        if (content == null || viewport == null)
            return false;

        float view = viewport.rect.height;
        float offset = content.anchoredPosition.y;
        float max = Mathf.Max(0f, content.rect.height - view);
        if (direction > 0 ? offset >= max - 0.5f : offset <= 0.5f)
            return false;

        if (next != null)
        {
            next.GetWorldCorners(Corners);
            float top = content.InverseTransformPoint(Corners[1]).y;
            float bottom = content.InverseTransformPoint(Corners[0]).y;
            bool visible = direction > 0 ? top > -offset - view + PadBottom : bottom < -offset - PadBottom;
            if (visible)
                return false;
        }

        offset = Mathf.Clamp(offset + direction * Mathf.Max(MinStep, view * StepShare), 0f, max);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
        scroll.velocity = Vector2.zero;
        return true;
    }
}
