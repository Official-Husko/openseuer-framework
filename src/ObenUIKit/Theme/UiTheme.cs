using TMPro;
using UnityEngine;
using UnityEngine.Audio;

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

    public AudioClip HoverClip;
    public AudioClip PressClip;
    public AudioMixerGroup Mixer;
    public float HoverVolume = 0.5f;
    public float PressVolume = 1f;

    public UiTheme Copy() => (UiTheme)MemberwiseClone();
}
