using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace ObenUIKit.Theme;

/// <summary>How much of the game's look has been captured so far.</summary>
public enum ThemeLevel
{
    /// <summary>Nothing captured yet; built-in values.</summary>
    Fallback,
    /// <summary>Taken from the main menu's sign.</summary>
    MainMenu,
    /// <summary>Taken from a level's pause menu.</summary>
    World,
}

/// <summary>
/// The game's look as plain values: fonts, colours, sprites and sounds read off the game's own
/// menus. Kit controls are built from these; game objects are never cloned for content.
/// The game renders in linear colour space, so the defaults below are linear values: a grey
/// that should look 25% bright is about 0.05 here.
/// </summary>
public sealed class UiTheme
{
    public ThemeLevel Level;

    public TMP_FontAsset Font;
    public Material FontMaterial;
    /// <summary>The font of the "&lt;" and "&gt;" arrows on option rows.</summary>
    public TMP_FontAsset ArrowFont;

    public float TitleSize = 34f;
    public float HeaderSize = 26f;
    public float TextSize = 24f;
    public float ButtonSize = 28f;

    public Color Text = new Color(0.86f, 0.86f, 0.86f, 1f);
    public Color TextHover = new Color(0.86f, 0.27f, 0.27f, 1f);
    public Color TextPress = new Color(0.55f, 0.08f, 0.08f, 1f);
    public Color TextDisabled = new Color(0.45f, 0.45f, 0.45f, 1f);
    public Color TextMuted = new Color(0.6f, 0.6f, 0.6f, 1f);

    public Sprite RowSprite;
    public Color RowHover = new Color(0.53f, 0.01f, 0.01f, 0.85f);
    public Sprite HeaderSprite;
    public Color Header = new Color(0.05f, 0.05f, 0.05f, 1f);

    public Color Panel = new Color(0f, 0f, 0f, 1f);
    public Color PanelEdge = new Color(0.08f, 0.08f, 0.08f, 1f);
    public Color Dim = new Color(0f, 0f, 0f, 0.6f);

    public Sprite SliderHandle;
    public Color SliderTrack = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color SliderFill = new Color(1f, 0.26f, 0.26f, 0.85f);

    /// <summary>The developer console's input field look: a translucent grey box with a pale blue selection.</summary>
    public Color FieldBackground = new Color(0.153f, 0.153f, 0.153f, 0.48f);
    public Color FieldSelection = new Color(0.659f, 0.808f, 1f, 0.75f);
    public float CaretWidth = 1f;
    public float CaretBlinkRate = 0.85f;

    public AudioClip HoverClip;
    public AudioClip PressClip;
    public AudioMixerGroup Mixer;
    public float HoverVolume = 0.5f;
    public float PressVolume = 1f;

    /// <summary>Muted text (hints, descriptions): a little smaller than <see cref="TextSize"/>.</summary>
    public float MutedSize => TextSize * 0.85f;

    /// <summary>The game's red for what is picked or active: a slider's fill.</summary>
    public Color Accent => SliderFill;

    /// <summary>The accent under the mouse or the highlight: the red text of a hovered menu button.</summary>
    public Color AccentHover => TextHover;

    /// <summary>The accent while pressed.</summary>
    public Color AccentPressed => TextPress;

    /// <summary>
    /// <paramref name="glyph"/> as text when the theme's font draws it, else <paramref name="fallback"/>. The menu
    /// font has some characters that come out as something else ("&lt;", "&gt;", "»", "›"); those count as missing.
    /// </summary>
    public string Glyph(char glyph, string fallback)
    {
        if (glyph == '<' || glyph == '>' || glyph == '\u00BB' || glyph == '\u203A' || glyph == '\u00AB' || glyph == '\u2039')
            return fallback;
        return Font != null && Font.HasCharacter(glyph) ? glyph.ToString() : fallback;
    }

    /// <summary>
    /// A uGUI colour block for a Selectable of your own in the kit's manner: normal, highlighted (also when
    /// selected), pressed, and a half-transparent normal when disabled, with a short fade.
    /// </summary>
    public static ColorBlock Blocks(Color normal, Color highlighted, Color pressed)
    {
        var block = ColorBlock.defaultColorBlock;
        block.normalColor = normal;
        block.highlightedColor = highlighted;
        block.selectedColor = highlighted;
        block.pressedColor = pressed;
        block.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.5f);
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.08f;
        return block;
    }

    public UiTheme Copy() => (UiTheme)MemberwiseClone();
}
