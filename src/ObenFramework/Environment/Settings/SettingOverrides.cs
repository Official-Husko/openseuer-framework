using System.Collections.Generic;
using ObenLoader;

namespace ObenFramework.Env;

/// <summary>
/// The option overrides mods hold. The newest value per option is copied into plain fields so the
/// per-frame camera hook reads a bool and a float. Nothing here writes the options files.
/// </summary>
internal static class SettingOverrides
{
    private static readonly OwnedValues<float> Fov = new OwnedValues<float> { Released = _ => SyncFov() };
    private static readonly OwnedValues<int> Brightness = new OwnedValues<int> { Released = _ => ApplyBrightness() };
    private static readonly OwnedValues<int> Sensitivity = new OwnedValues<int> { Released = _ => ApplySensitivity() };

    /// <summary>True while a field of view override is in place; <see cref="FovValue"/> holds it.</summary>
    public static bool HasFov;
    public static float FovValue;

    public static bool HasBrightness => !Brightness.IsEmpty;

    public static bool HasSensitivity => !Sensitivity.IsEmpty;

    public static bool TryFov(out float fov)
    {
        fov = FovValue;
        return HasFov;
    }

    public static bool TryBrightness(out int percent) => Brightness.TryGet("", out percent);

    public static bool TrySensitivity(out int value) => Sensitivity.TryGet("", out value);

    public static List<OwnedValue<float>> FovEntries => Fov.Entries;

    public static List<OwnedValue<int>> BrightnessEntries => Brightness.Entries;

    public static List<OwnedValue<int>> SensitivityEntries => Sensitivity.Entries;

    public static void SetFov(ObenMod owner, float degrees)
    {
        Fov.Set(owner, owner.Info.Id, "", degrees);
        SyncFov();
    }

    public static bool ClearFov(ObenMod owner)
    {
        bool removed = Fov.Remove(owner, "");
        SyncFov();
        return removed;
    }

    public static void SetBrightness(ObenMod owner, int percent)
    {
        Brightness.Set(owner, owner.Info.Id, "", percent);
        ApplyBrightness();
    }

    public static bool ClearBrightness(ObenMod owner)
    {
        bool removed = Brightness.Remove(owner, "");
        ApplyBrightness();
        return removed;
    }

    public static void SetSensitivity(ObenMod owner, int value)
    {
        Sensitivity.Set(owner, owner.Info.Id, "", value);
        ApplySensitivity();
    }

    public static bool ClearSensitivity(ObenMod owner)
    {
        bool removed = Sensitivity.Remove(owner, "");
        ApplySensitivity();
        return removed;
    }

    /// <summary>A new scene brought new cameras and effects: push the overrides into them.</summary>
    public static void Reapply()
    {
        if (HasBrightness)
            ApplyBrightness();
        if (HasSensitivity)
            ApplySensitivity();
    }

    /// <summary>The framework unloads: drop every override and give the game its own values back.</summary>
    public static void ReleaseAll()
    {
        bool brightness = HasBrightness, sensitivity = HasSensitivity;
        Fov.Clear();
        Brightness.Clear();
        Sensitivity.Clear();
        SyncFov();
        if (brightness)
            ApplyBrightness();
        if (sensitivity)
            ApplySensitivity();
    }

    /// <summary>The camera multiplier the game derives from a slider value (an integer division, like <c>SetControls</c>).</summary>
    public static float CameraSensitivity(int value) => value / 100;

    private static void SyncFov()
    {
        HasFov = Fov.TryGet("", out FovValue);
    }

    // ChangeBrightness is patched to use the override, so asking for the saved value applies whichever is current.
    private static void ApplyBrightness()
    {
        var effects = EffectsController.instance;
        var display = DisplaySave.instance;
        if (effects != null && display != null)
            effects.ChangeBrightness(display.displaySave.brightness / 100f);
    }

    private static void ApplySensitivity()
    {
        var camera = ThirdPersonCameraController.instance;
        if (camera == null)
            return;
        if (SetControls.instance != null && ControlsSave.instance != null)
            SetControls.instance.SetSensitivity();
        else if (TrySensitivity(out int value))
            camera.mouseSensitivitySetting = CameraSensitivity(value);
        else if (ControlsSave.instance != null && ControlsSave.instance.controlsSave != null)
            camera.mouseSensitivitySetting = CameraSensitivity(ControlsSave.instance.controlsSave.mouseSensitivity);
    }
}
