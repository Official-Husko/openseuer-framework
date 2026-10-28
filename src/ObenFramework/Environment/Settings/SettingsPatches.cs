using System;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>Keeps the camera on an overridden field of view. <c>SetCameraFOV</c> resets the camera from the saved option every frame.</summary>
[HarmonyPatch]
internal static class FovPatches
{
    private static SetCameraFOV _host;
    private static Camera _camera;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SetCameraFOV), "Update")]
    private static bool BeforeUpdate(SetCameraFOV __instance) => Apply(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SetCameraFOV), nameof(SetCameraFOV.SetFOV))]
    private static bool BeforeSet(SetCameraFOV __instance) => Apply(__instance);

    public static void Reset()
    {
        _host = null;
        _camera = null;
    }

    // Returns false (skip the game's method) while an override holds the camera.
    private static bool Apply(SetCameraFOV instance)
    {
        if (!SettingOverrides.HasFov)
            return true;
        try
        {
            if (_host != instance || _camera == null)
            {
                _host = instance;
                _camera = instance.GetComponent<Camera>();
            }
            if (_camera == null)
                return true;
            if (_camera.fieldOfView != SettingOverrides.FovValue)
                _camera.fieldOfView = SettingOverrides.FovValue;
            return false;
        }
        catch (Exception e)
        {
            WorldLog.Failed("Field of view override", e);
            return true;
        }
    }
}

/// <summary>Swaps in an overridden brightness and mouse sensitivity wherever the game applies the saved ones.</summary>
[HarmonyPatch]
internal static class OptionOverridePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EffectsController), nameof(EffectsController.ChangeBrightness))]
    private static void Brightness(ref float value)
    {
        if (SettingOverrides.TryBrightness(out int percent))
            value = percent / 100f;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SetControls), nameof(SetControls.SetSensitivity))]
    private static void Sensitivity()
    {
        try
        {
            var camera = ThirdPersonCameraController.instance;
            if (camera != null && SettingOverrides.TrySensitivity(out int value))
                camera.mouseSensitivitySetting = SettingOverrides.CameraSensitivity(value);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Mouse sensitivity override", e);
        }
    }
}

/// <summary>
/// Raises <see cref="GameSettings.Changed"/> when an options file is written. The menu's apply buttons
/// write one option at a time; those are folded into one event per apply.
/// </summary>
[HarmonyPatch]
internal static class SettingsChangedPatches
{
    private static int _batch;
    private static bool _display, _graphics;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(OptionsMenu), nameof(OptionsMenu.applyDisplaySettings))]
    private static void DisplayApplyStart() => _batch++;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(OptionsMenu), nameof(OptionsMenu.applyDisplaySettings))]
    private static void DisplayApplyEnd() => EndBatch();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(OptionsMenu), nameof(OptionsMenu.applyQualitySettings))]
    private static void QualityApplyStart() => _batch++;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(OptionsMenu), nameof(OptionsMenu.applyQualitySettings))]
    private static void QualityApplyEnd() => EndBatch();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DisplaySave), nameof(DisplaySave.SaveDisplay))]
    private static void DisplaySaved()
    {
        if (_batch > 0)
            _display = true;
        else
            Raise(SettingsArea.Display);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QualitySave), nameof(QualitySave.saveQualityAll))]
    private static void QualitySaved()
    {
        if (_batch > 0)
            _graphics = true;
        else
            Raise(SettingsArea.Graphics);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ControlsSave), nameof(ControlsSave.SaveControls))]
    private static void ControlsSaved() => Raise(SettingsArea.Controls);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(VolumeSave), nameof(VolumeSave.SaveVolume))]
    private static void VolumeSaved() => Raise(SettingsArea.Volume);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HintOverlay), nameof(HintOverlay.HintsEnabled), MethodType.Setter)]
    private static void HintsSaved() => Raise(SettingsArea.Hints);

    public static void Reset() => _batch = 0;

    private static void EndBatch()
    {
        if (_batch > 0)
            _batch--;
        if (_batch > 0)
            return;
        bool display = _display, graphics = _graphics;
        _display = _graphics = false;
        if (display)
            Raise(SettingsArea.Display);
        if (graphics)
            Raise(SettingsArea.Graphics);
    }

    private static void Raise(SettingsArea area)
    {
        try
        {
            if (GameSettings.Changed.HasSubscribers)
                GameSettings.Changed.Raise(area);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Settings changed hook", e);
        }
    }
}
