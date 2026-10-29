using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Windows;

/// <summary>
/// Builds a window's frame the way the pause menu looks: a black column with grey edges, a title on
/// top, a scrolling body and a footer for buttons. The centred column dims the scene behind; docked
/// columns sit on the left or right edge, a floating box can be dragged by its title, and a full window
/// covers the screen less a margin, with an optional side panel left of the body.
/// </summary>
internal static class WindowLayout
{
    public const float TitleHeight = 120f;
    public const float FooterHeight = 110f;
    public const float EdgeWidth = 4f;
    private const float DockMargin = 24f;
    private const float BodyMargin = 24f;

    public sealed class Parts
    {
        public RectTransform Frame;
        public RectTransform Column;
        public TextMeshProUGUI Title;
        public ScrollRect Scroll;
        public RectTransform Content;
        public RectTransform Footer;

        /// <summary>The side panel's scrolling column; full windows with a side panel only.</summary>
        public RectTransform SideContent;

        /// <summary>The title's drag handle; floating windows only.</summary>
        public WindowDrag Drag;
    }

    public static Parts Build(RectTransform root, UiTheme theme, string name, WindowOptions options)
    {
        var frame = UiParts.Stretch(UiParts.Child(root, name));
        var dim = frame.gameObject.AddComponent<Image>();
        dim.color = options.Dims ? theme.Dim : Color.clear;
        // A dimmed window keeps clicks from falling through to the game behind; the others leave the rest of the screen alone.
        dim.raycastTarget = options.Dims;

        var column = UiParts.Image(frame, "Column", null, theme.Panel, raycast: true).rectTransform;
        float height = options.ResolvedHeight;
        bool box = height > 0f;
        Place(column, options, height);
        Edge(column, theme, left: true);
        Edge(column, theme, left: false);
        if (box || options.Dock == WindowDock.Full)
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

        WindowDrag drag = null;
        if (options.Dock == WindowDock.Floating)
        {
            title.raycastTarget = true;
            drag = title.gameObject.AddComponent<WindowDrag>();
        }

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

        var parts = new Parts { Frame = frame, Column = column, Title = title, Footer = footer, Drag = drag };
        float left = BodyMargin;
        if (options.Dock == WindowDock.Full && options.Side != null)
        {
            var side = ScrollArea.Make(column, theme, "Side");
            float width = Mathf.Max(120f, options.SideWidth);
            Region(side.Region, BodyMargin, width);
            parts.SideContent = side.Content;
            var line = UiParts.Image(column, "SideEdge", null, theme.PanelEdge).rectTransform;
            Region(line, BodyMargin + width + BodyMargin * 0.5f, EdgeWidth);
            left = BodyMargin * 2f + width;
        }
        var body = ScrollArea.Make(column, theme, "Body");
        UiParts.Stretch(body.Region, left, TitleHeight, BodyMargin - ScrollArea.BarRoom, FooterHeight);
        parts.Scroll = body.Scroll;
        parts.Content = body.Content;
        return parts;
    }

    // A strip between the title and the footer, from the column's left edge.
    private static void Region(RectTransform rect, float left, float width)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(left, FooterHeight);
        rect.offsetMax = new Vector2(left + width, -TitleHeight);
    }

    private static void Place(RectTransform column, WindowOptions options, float height)
    {
        bool box = height > 0f;
        float width = options.Width;
        switch (options.Dock)
        {
            case WindowDock.Left:
            case WindowDock.Right:
                float x = options.Dock == WindowDock.Left ? 0f : 1f;
                float top = box ? 0f : Mathf.Max(0f, options.Top);
                column.anchorMin = new Vector2(x, box ? 0.5f : 0f);
                column.anchorMax = new Vector2(x, box ? 0.5f : 1f);
                column.pivot = new Vector2(x, 0.5f);
                column.sizeDelta = new Vector2(width, box ? height : -top);
                column.anchoredPosition = new Vector2(options.Dock == WindowDock.Left ? DockMargin : -DockMargin, -top * 0.5f);
                break;
            case WindowDock.Full:
                column.anchorMin = Vector2.zero;
                column.anchorMax = Vector2.one;
                column.offsetMin = new Vector2(DockMargin, DockMargin);
                column.offsetMax = new Vector2(-DockMargin, -DockMargin);
                break;
            case WindowDock.Floating:
                column.anchorMin = column.anchorMax = column.pivot = new Vector2(0.5f, 0.5f);
                column.sizeDelta = new Vector2(width, height);
                column.anchoredPosition = options.Position;
                break;
            default:
                column.anchorMin = new Vector2(0.5f, box ? 0.5f : 0f);
                column.anchorMax = new Vector2(0.5f, box ? 0.5f : 1f);
                column.sizeDelta = new Vector2(width, box ? height : 0f);
                break;
        }
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
