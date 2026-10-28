using ObenUIKit.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>Builds a uGUI slider in the game's colours: grey track, red fill, the game's handle sprite.</summary>
internal static class SliderParts
{
    public static Slider Build(RectTransform row, UiTheme theme)
    {
        var area = UiParts.Child(row, "Slider");
        area.anchorMin = new Vector2(0.25f, 0.5f);
        area.anchorMax = new Vector2(0.94f, 0.5f);
        area.sizeDelta = new Vector2(0f, 14f);

        var track = UiParts.Stretch(UiParts.Image(area, "Track", null, theme.SliderTrack).rectTransform);
        track.sizeDelta = new Vector2(0f, -6f);

        var fillArea = UiParts.Stretch(UiParts.Child(area, "Fill Area"), 0f, 0f, 0f, 0f);
        var fill = UiParts.Image(fillArea, "Fill", null, theme.SliderFill);
        fill.rectTransform.sizeDelta = new Vector2(0f, -6f);

        var handleArea = UiParts.Stretch(UiParts.Child(area, "Handle Area"), 10f, 0f, 10f, 0f);
        var handle = UiParts.Image(handleArea, "Handle", theme.SliderHandle, theme.SliderHandle != null ? Color.white : theme.Text, raycast: true);
        handle.rectTransform.sizeDelta = new Vector2(30f, 30f);
        handle.preserveAspect = true;

        var slider = area.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        var colors = slider.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.85f, 1f);
        slider.colors = colors;
        area.gameObject.AddComponent<KitSliderRelease>();
        return slider;
    }
}

/// <summary>
/// A clicked slider stays selected and the event system would then move it on arrow keys too,
/// on top of the kit's own keyboard navigation. Letting go gives the selection back.
/// </summary>
internal sealed class KitSliderRelease : MonoBehaviour, IPointerUpHandler
{
    public void OnPointerUp(PointerEventData data)
    {
        var events = EventSystem.current;
        if (events != null && events.currentSelectedGameObject == gameObject)
            events.SetSelectedGameObject(null);
    }
}
