using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Theme;

/// <summary>Copies values off the game's own UI components into a <see cref="UiTheme"/>.</summary>
internal static class ThemeReader
{
    /// <summary>A menu text button: font, text colours and its hover and press sounds.</summary>
    public static bool FromTextButton(UiTheme theme, TextButtonColorEffectTMP button)
    {
        if (button == null)
            return false;
        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null && text.font != null)
        {
            theme.Font = text.font;
            theme.FontMaterial = text.fontSharedMaterial;
        }
        theme.Text = Opaque(Native.ButtonDefault(button));
        theme.TextHover = Opaque(Native.ButtonHover(button));
        theme.TextPress = Opaque(Native.ButtonPress(button));
        theme.TextDisabled = Opaque(Native.ButtonDisabled(button));
        Sound(button.hoverSound, ref theme.HoverClip, ref theme.HoverVolume, theme);
        Sound(button.pressSound, ref theme.PressClip, ref theme.PressVolume, theme);
        return true;
    }

    /// <summary>An options row (<see cref="OptionsButton"/>): row sprite and hover colour, header bar, arrow font.</summary>
    public static bool FromOptionRow(UiTheme theme, OptionsButton row)
    {
        if (row == null)
            return false;
        var image = Native.RowImage(row);
        if (image != null && image.sprite != null)
            theme.RowSprite = image.sprite;
        var hover = Native.RowHover(row);
        if (hover.a > 0.01f)
            theme.RowHover = hover;

        var title = row.transform.parent != null ? row.transform.parent.Find("Option Title") : null;
        var bar = title != null ? title.GetComponent<Image>() : null;
        // Only a real sprite is worth taking; without one the bar's colour is meant for the sign's lighting.
        if (bar != null && bar.sprite != null)
        {
            theme.HeaderSprite = bar.sprite;
            theme.Header = bar.color;
        }

        foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.text == "<" && text.font != null)
                theme.ArrowFont = text.font;
        }
        return true;
    }

    /// <summary>An options slider: the handle sprite.</summary>
    public static bool FromSlider(UiTheme theme, Slider slider)
    {
        if (slider == null || slider.handleRect == null)
            return false;
        var handle = slider.handleRect.GetComponent<Image>();
        if (handle != null && handle.sprite != null)
            theme.SliderHandle = handle.sprite;
        return true;
    }

    private static void Sound(SoundInfo sound, ref AudioClip clip, ref float volume, UiTheme theme)
    {
        if (sound == null || sound.Clip == null)
            return;
        clip = sound.Clip;
        volume = sound.SoundVolume;
        if (sound.Mixer != null)
            theme.Mixer = sound.Mixer;
    }

    // Some colours are animated in from alpha 0; the kit wants the visible colour.
    private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
}
