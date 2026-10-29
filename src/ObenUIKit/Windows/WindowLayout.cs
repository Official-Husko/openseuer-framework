using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Windows;

/// <summary>
/// Builds a window's frame the way the pause menu looks: a black column with grey edges, a title on
/// top, a scrolling body and a footer for buttons. The centred column dims the scene behind; docked
/// columns sit on the left or right edge, and a floating box can be dragged by its title.
/// </summary>
internal static class WindowLayout
{
    public const float TitleHeight = 120f;
    public const float FooterHeight = 110f;
    public const float EdgeWidth = 4f;
    private const float DockMargin = 24f;

    public sealed class Parts
    {
        public RectTransform Frame;
        public RectTransform Column;
        public TextMeshProUGUI Title;
        public ScrollRect Scroll;
        public RectTransform Content;
        public RectTransform Footer;

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

        var (scroll, content) = Body(column, theme);
        return new Parts { Frame = frame, Column = column, Title = title, Scroll = scroll, Content = content, Footer = footer, Drag = drag };
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
                column.anchorMin = new Vector2(x, box ? 0.5f : 0f);
                column.anchorMax = new Vector2(x, box ? 0.5f : 1f);
                column.pivot = new Vector2(x, 0.5f);
                column.sizeDelta = new Vector2(width, box ? height : 0f);
                column.anchoredPosition = new Vector2(options.Dock == WindowDock.Left ? DockMargin : -DockMargin, 0f);
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

    private static (ScrollRect, RectTransform) Body(RectTransform column, UiTheme theme)
    {
        var body = UiParts.Stretch(UiParts.Child(column, "Body"), 24f, TitleHeight, 24f, FooterHeight);
        body.gameObject.AddComponent<RectMask2D>();
        // An invisible background, so the mouse wheel scrolls over text and the gaps between controls too,
        // not only over the controls that take clicks.
        body.gameObject.AddComponent<Image>().color = Color.clear;
        var scroll = body.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.verticalScrollbar = Bar(column, theme);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

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

    // A thin bar in the body's right margin that shows how far down the page is and can be dragged. Hidden
    // while everything fits.
    private static Scrollbar Bar(RectTransform column, UiTheme theme)
    {
        var track = UiParts.Image(column, "Scrollbar", null, theme.SliderTrack, raycast: true).rectTransform;
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = new Vector2(1f, 1f);
        track.pivot = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(-16f, FooterHeight);
        track.offsetMax = new Vector2(-10f, -TitleHeight);

        var area = UiParts.Stretch(UiParts.Child(track, "Area"));
        var handle = UiParts.Image(area, "Handle", null, theme.TextMuted, raycast: true);
        UiParts.Stretch(handle.rectTransform);

        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.transition = Selectable.Transition.None;
        // The kit moves the focus itself; the game's event system shouldn't select the bar.
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        return bar;
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
