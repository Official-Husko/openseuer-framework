using System;
using ObenFramework.World;
using ObenLoader;

namespace ObenFramework.Env;

/// <summary>
/// The player's options: read them, react when they change, and override a few for as long as a mod needs
/// to. Overrides (<see cref="OverrideFov"/>, <see cref="OverrideBrightness"/>, <see cref="OverrideMouseSensitivity"/>)
/// only change what the game shows; the options files keep the player's choice and the override goes away
/// when the mod clears it or unloads. Only the <c>Save*</c> methods write the player's options files, the
/// same way the options menu does. The options objects live on from the main menu on, so all of this works
/// there too.
/// </summary>
public static class GameSettings
{
    public const int MinFov = 30;
    public const int MaxFov = 120;
    public const int MinBrightness = 0;
    public const int MaxBrightness = 200;
    public const int MinMouseSensitivity = 100;
    public const int MaxMouseSensitivity = 10000;

    /// <summary>An option was changed and written to the player's options files (by the menu or a <c>Save*</c> call).</summary>
    public static readonly WorldEvent<SettingsArea> Changed = new WorldEvent<SettingsArea>(nameof(Changed));

    /// <summary>The display options, or null before the game read them.</summary>
    public static DisplaySettings Display
    {
        get
        {
            var save = DisplaySave.instance;
            if (save == null || save.displaySave == null)
                return null;
            var d = save.displaySave;
            return new DisplaySettings(d.screenWidth, d.screenHeight, d.preferredRefreshRate, ModeOf(d.screenMode), d.VSync, d.fov, d.brightness, d.runInBackground, d.language);
        }
    }

    public static GraphicsQuality Graphics
    {
        get
        {
            var save = QualitySave.instance;
            if (save == null || save.qualitySave == null)
                return null;
            var q = save.qualitySave;
            return new GraphicsQuality(q.qualityS, q.custom, q.shadowQ, q.shadowR, q.textureR, q.SSAO, q.antialasing, q.postFX);
        }
    }

    public static ControlSettings Controls
    {
        get
        {
            var save = ControlsSave.instance;
            if (save == null || save.controlsSave == null)
                return null;
            var c = save.controlsSave;
            string invert = c.invertMouse ?? "";
            return new ControlSettings(c.mouseSensitivity, invert == "X" || invert == "X + Y", invert == "Y" || invert == "X + Y", c.toggleSprint, c.toggleCrouch);
        }
    }

    public static VolumeSettings Volume
    {
        get
        {
            var save = VolumeSave.instance;
            if (save == null || save.soundSave == null)
                return null;
            var v = save.soundSave;
            return new VolumeSettings(v.mode, v.master, v.sound, v.music, v.nasty);
        }
    }

    /// <summary>The on-screen key hints (the game keeps this one in PlayerPrefs).</summary>
    public static bool HintsEnabled => HintOverlay.HintsEnabled;

    /// <summary>The field of view the camera uses now: a mod override if there is one, else the saved option.</summary>
    public static float EffectiveFov => SettingOverrides.TryFov(out float fov) ? fov : Display != null ? Display.Fov : 0f;

    /// <summary>Sets the camera's field of view (30 to 120 degrees) until <paramref name="owner"/> clears it or unloads. The newest override wins.</summary>
    public static void OverrideFov(ObenMod owner, float degrees)
    {
        RequireOwner(owner);
        RequireFinite(degrees, nameof(degrees));
        SettingOverrides.SetFov(owner, Math.Max(MinFov, Math.Min(MaxFov, degrees)));
    }

    public static bool ClearFov(ObenMod owner) => owner != null && SettingOverrides.ClearFov(owner);

    /// <summary>Sets the brightness in percent (0 to 200) until <paramref name="owner"/> clears it or unloads.</summary>
    public static void OverrideBrightness(ObenMod owner, int percent)
    {
        RequireOwner(owner);
        SettingOverrides.SetBrightness(owner, Math.Max(MinBrightness, Math.Min(MaxBrightness, percent)));
    }

    public static bool ClearBrightness(ObenMod owner) => owner != null && SettingOverrides.ClearBrightness(owner);

    /// <summary>Sets the mouse sensitivity (the slider's 100 to 10000; 900 is the default) until <paramref name="owner"/> clears it or unloads.</summary>
    public static void OverrideMouseSensitivity(ObenMod owner, int value)
    {
        RequireOwner(owner);
        SettingOverrides.SetSensitivity(owner, Math.Max(MinMouseSensitivity, Math.Min(MaxMouseSensitivity, value)));
    }

    public static bool ClearMouseSensitivity(ObenMod owner) => owner != null && SettingOverrides.ClearSensitivity(owner);

    /// <summary>Writes the field of view into the player's options, like the options slider.</summary>
    public static void SaveFov(int degrees)
    {
        var save = Require(DisplaySave.instance, "display");
        save.SaveDisplay("Fov", "", false, Math.Max(MinFov, Math.Min(MaxFov, degrees)));
    }

    /// <summary>Writes the brightness into the player's options and applies it, like the options slider.</summary>
    public static void SaveBrightness(int percent)
    {
        var save = Require(DisplaySave.instance, "display");
        percent = Math.Max(MinBrightness, Math.Min(MaxBrightness, percent));
        save.SaveDisplay("Brightness", "", false, percent);
        if (EffectsController.instance != null)
            EffectsController.instance.ChangeBrightness(percent / 100f);
    }

    /// <summary>Writes the mouse sensitivity into the player's options and applies it, like the options slider.</summary>
    public static void SaveMouseSensitivity(int value)
    {
        var save = Require(ControlsSave.instance, "control");
        save.SaveControls("Mouse Sensitivity", Math.Max(MinMouseSensitivity, Math.Min(MaxMouseSensitivity, value)));
        if (SetControls.instance != null && ThirdPersonCameraController.instance != null)
            SetControls.instance.SetSensitivity();
    }

    /// <summary>Turns the on-screen key hints on or off for good, like the hints key.</summary>
    public static void SaveHintsEnabled(bool enabled) => HintOverlay.HintsEnabled = enabled;

    internal static ScreenMode ModeOf(string mode) =>
        mode == "Full Screen" ? ScreenMode.FullScreen : mode == "Borderless" ? ScreenMode.Borderless : ScreenMode.Windowed;

    private static T Require<T>(T save, string what) where T : UnityEngine.Object
    {
        if (save == null)
            throw new InvalidOperationException($"The game's {what} options are not loaded yet.");
        return save;
    }

    private static void RequireOwner(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
    }

    private static void RequireFinite(float value, string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentException("The value must be a finite number.", name);
    }
}
