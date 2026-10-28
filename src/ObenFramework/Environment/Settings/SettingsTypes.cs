namespace ObenFramework.Env;

/// <summary>Which group of options changed, as raised by <see cref="GameSettings.Changed"/>.</summary>
public enum SettingsArea
{
    Display,
    Graphics,
    Controls,
    Volume,
    Hints,
}

public enum ScreenMode
{
    Windowed,
    Borderless,
    FullScreen,
}

/// <summary>The display options as saved in the player's <c>Display.json</c>.</summary>
public sealed class DisplaySettings
{
    internal DisplaySettings(int width, int height, int refreshRate, ScreenMode mode, bool vSync, int fov, int brightness, bool runInBackground, string language)
    {
        Width = width;
        Height = height;
        RefreshRate = refreshRate;
        Mode = mode;
        VSync = vSync;
        Fov = fov;
        Brightness = brightness;
        RunInBackground = runInBackground;
        Language = language;
    }

    /// <summary>0 until the player picked a resolution.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>The frame rate cap; 0 means the game's default of 60.</summary>
    public int RefreshRate { get; }

    public ScreenMode Mode { get; }

    public bool VSync { get; }

    /// <summary>Field of view in degrees, as saved (a mod override does not change it).</summary>
    public int Fov { get; }

    /// <summary>Brightness in percent, 100 by default.</summary>
    public int Brightness { get; }

    public bool RunInBackground { get; }

    public string Language { get; }
}

/// <summary>The graphics options as saved in the player's <c>Quality.json</c>. Levels are indices into the options menu's lists.</summary>
public sealed class GraphicsQuality
{
    internal GraphicsQuality(int preset, bool custom, int shadowQuality, int shadowResolution, int textureResolution, int ssao, int antialiasing, int postFx)
    {
        Preset = preset;
        Custom = custom;
        ShadowQuality = shadowQuality;
        ShadowResolution = shadowResolution;
        TextureResolution = textureResolution;
        Ssao = ssao;
        Antialiasing = antialiasing;
        PostFx = postFx;
    }

    /// <summary>The quality preset (Unity quality level).</summary>
    public int Preset { get; }

    /// <summary>True when the player changed single options after picking the preset.</summary>
    public bool Custom { get; }

    public int ShadowQuality { get; }

    public int ShadowResolution { get; }

    public int TextureResolution { get; }

    public int Ssao { get; }

    public int Antialiasing { get; }

    public int PostFx { get; }
}

/// <summary>The control options as saved in the player's <c>Controls.json</c>.</summary>
public sealed class ControlSettings
{
    internal ControlSettings(int mouseSensitivity, bool invertX, bool invertY, bool toggleSprint, bool toggleCrouch)
    {
        MouseSensitivity = mouseSensitivity;
        InvertX = invertX;
        InvertY = invertY;
        ToggleSprint = toggleSprint;
        ToggleCrouch = toggleCrouch;
    }

    /// <summary>The slider value (900 by default); the camera uses it divided by 100.</summary>
    public int MouseSensitivity { get; }

    public bool InvertX { get; }

    public bool InvertY { get; }

    public bool ToggleSprint { get; }

    public bool ToggleCrouch { get; }
}

/// <summary>The volume options as saved in the player's volume file. Volumes are mixer levels in decibels.</summary>
public sealed class VolumeSettings
{
    internal VolumeSettings(string speakerMode, float master, float sound, float music, float nasty)
    {
        SpeakerMode = speakerMode;
        Master = master;
        Sound = sound;
        Music = music;
        Nasty = nasty;
    }

    /// <summary>"Stereo", "Quad", "Surround", "Surround 5.1" or "Surround 7.1".</summary>
    public string SpeakerMode { get; }

    public float Master { get; }

    public float Sound { get; }

    public float Music { get; }

    /// <summary>The game's separate slider for unpleasant sounds.</summary>
    public float Nasty { get; }
}
