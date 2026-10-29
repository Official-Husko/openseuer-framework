using ObenUIKit.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>
/// A scrolling column of controls: a clipped view with an invisible background (so the mouse wheel works over
/// text and gaps, not only over controls), a vertical stack that grows with what is built into it, and a thin
/// bar on the right that shows the position, can be dragged, and hides while everything fits. The window body,
/// a full window's side panel and <see cref="UiBuilder.Scroll"/> are all one of these.
/// </summary>
internal static class ScrollArea
{
    /// <summary>Room on the right of the view for the bar.</summary>
    public const float BarRoom = 14f;

    public sealed class Parts
    {
        /// <summary>The whole area, bar included; the caller places it.</summary>
        public RectTransform Region;
        public ScrollRect Scroll;
        public RectTransform Content;
    }

    public static Parts Make(Transform parent, UiTheme theme, string name, float spacing = 10f)
    {
        var region = UiParts.Child(parent, name);
        var view = UiParts.Stretch(UiParts.Child(region, "View"), 0f, 0f, BarRoom, 0f);
        view.gameObject.AddComponent<RectMask2D>();
        view.gameObject.AddComponent<Image>().color = Color.clear;
        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var content = UiParts.Child(view, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = view;
        scroll.content = content;
        scroll.verticalScrollbar = Bar(region, theme);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return new Parts { Region = region, Scroll = scroll, Content = content };
    }

    private static Scrollbar Bar(RectTransform region, UiTheme theme)
    {
        var track = UiParts.Image(region, "Scrollbar", null, theme.SliderTrack, raycast: true).rectTransform;
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = new Vector2(1f, 1f);
        track.pivot = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(-6f, 0f);
        track.offsetMax = Vector2.zero;

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
}
