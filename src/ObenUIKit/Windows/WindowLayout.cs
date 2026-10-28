using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Windows;

/// <summary>
/// Builds a window's frame the way the pause menu looks: the scene dimmed behind, a black column
/// with grey edges in the middle, a title on top, a scrolling body and a footer for buttons.
/// </summary>
internal static class WindowLayout
{
    public const float TitleHeight = 120f;
    public const float FooterHeight = 110f;
    public const float EdgeWidth = 4f;

    public sealed class Parts
    {
        public RectTransform Frame;
        public TextMeshProUGUI Title;
        public ScrollRect Scroll;
        public RectTransform Content;
        public RectTransform Footer;
    }

    /// <summary>A <paramref name="height"/> of 0 gives the full-height column of the pause menu; anything else a centred box.</summary>
    public static Parts Build(RectTransform root, UiTheme theme, string name, float width, float height = 0f)
    {
        var frame = UiParts.Stretch(UiParts.Child(root, name));
        var dim = frame.gameObject.AddComponent<Image>();
        dim.color = theme.Dim;
        dim.raycastTarget = true; // Clicks never fall through to the game behind.

        var column = UiParts.Image(frame, "Column", null, theme.Panel, raycast: true).rectTransform;
        bool box = height > 0f;
        column.anchorMin = new Vector2(0.5f, box ? 0.5f : 0f);
        column.anchorMax = new Vector2(0.5f, box ? 0.5f : 1f);
        column.sizeDelta = new Vector2(width, box ? height : 0f);
        Edge(column, theme, left: true);
        Edge(column, theme, left: false);
        if (box)
        {
            Cap(column, theme, top: true);
            Cap(column, theme, top: false);
        }

        var title = UiParts.Text(column, theme, "", theme.TitleSize, theme.Text);
        UiParts.Stretch(title.rectTransform, 30f, 30f, 30f, 0f);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-60f, TitleHeight - 30f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.fontStyle = FontStyles.Underline;

        var footer = UiParts.Child(column, "Footer");
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, FooterHeight);
        var row = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 40f;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        row.childControlWidth = true;
        row.childControlHeight = true;

        var (scroll, content) = Body(column);
        return new Parts { Frame = frame, Title = title, Scroll = scroll, Content = content, Footer = footer };
    }

    private static (ScrollRect, RectTransform) Body(RectTransform column)
    {
        var body = UiParts.Stretch(UiParts.Child(column, "Body"), 24f, TitleHeight, 24f, FooterHeight);
        body.gameObject.AddComponent<RectMask2D>();
        var scroll = body.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var content = UiParts.Child(body, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = body;
        scroll.content = content;
        return (scroll, content);
    }

    private static void Cap(RectTransform column, UiTheme theme, bool top)
    {
        var cap = UiParts.Image(column, top ? "EdgeTop" : "EdgeBottom", null, theme.PanelEdge).rectTransform;
        cap.anchorMin = new Vector2(0f, top ? 1f : 0f);
        cap.anchorMax = new Vector2(1f, top ? 1f : 0f);
        cap.pivot = new Vector2(0.5f, top ? 1f : 0f);
        cap.sizeDelta = new Vector2(0f, EdgeWidth);
    }

    private static void Edge(RectTransform column, UiTheme theme, bool left)
    {
        var edge = UiParts.Image(column, left ? "EdgeLeft" : "EdgeRight", null, theme.PanelEdge).rectTransform;
        edge.anchorMin = new Vector2(left ? 0f : 1f, 0f);
        edge.anchorMax = new Vector2(left ? 0f : 1f, 1f);
        edge.pivot = new Vector2(left ? 0f : 1f, 0.5f);
        edge.sizeDelta = new Vector2(EdgeWidth, 0f);
    }
}
