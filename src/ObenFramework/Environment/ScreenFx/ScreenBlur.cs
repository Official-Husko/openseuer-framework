// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using BeautifyEffect;
using HarmonyLib;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// A full-screen blur through the game's own post effect (Beautify, the one the game blurs menus with).
/// While a mod holds a blur, every camera's Beautify has blur on at the mod's strength; the game's own
/// blur switches and profile changes are remembered underneath and come back when the last hold ends.
/// </summary>
internal static class ScreenBlur
{
    /// <summary>Beautify's blur intensity at amount 1.</summary>
    private const float MaxIntensity = 4f;

    private static readonly AccessTools.FieldRef<EffectsController, List<EffectBeautify>> BeautifyList =
        AccessTools.FieldRefAccess<EffectsController, List<EffectBeautify>>("beautifyList");

    private static readonly FadeTrack Track = new FadeTrack(1);
    private static readonly float[] Target = new float[1];
    private static readonly float[] Target0 = new float[1];
    private static readonly OwnedValues<float> Values = new OwnedValues<float> { Released = _ => Retarget(0f) };

    // The cameras' own blur settings, captured the first time a hold touched them.
    private static readonly List<Beautify> Touched = new List<Beautify>();
    private static readonly List<float> TouchedIntensity = new List<float>();
    private static bool _gameWantsBlur;

    public static bool Any => !Values.IsEmpty;

    public static void Set(ObenMod owner, float amount, float fadeSeconds)
    {
        Values.Set(owner, owner.Info.Id, "", amount);
        Retarget(fadeSeconds);
    }

    public static bool Clear(ObenMod owner, float fadeSeconds)
    {
        if (!Values.Remove(owner, ""))
            return false;
        Retarget(fadeSeconds);
        return true;
    }

    public static bool Step(float seconds)
    {
        bool running = Track.Step(seconds);
        Refresh();
        return running;
    }

    /// <summary>The game switched its blur on or off (menus, the inventory). Remember its wish and keep ours on top.</summary>
    public static void GameSwitched(bool on)
    {
        _gameWantsBlur = on;
        if (Touched.Count > 0)
            Refresh();
    }

    /// <summary>The game swapped Beautify profiles, which resets the blur values: take them as the new values underneath.</summary>
    public static void ProfileChanged()
    {
        if (Touched.Count == 0)
            return;
        for (int i = 0; i < Touched.Count; i++)
        {
            if (Touched[i] == null)
                continue;
            _gameWantsBlur = Touched[i].blur;
            TouchedIntensity[i] = Touched[i].blurIntensity;
        }
        Refresh();
    }

    /// <summary>The scene is going away with its cameras; nothing to restore on them.</summary>
    public static void ForgetScene()
    {
        Touched.Clear();
        TouchedIntensity.Clear();
    }

    /// <summary>Applies to the new scene's cameras.</summary>
    public static void Reapply()
    {
        if (Any || Track.Current[0] > 0f)
            Refresh();
    }

    public static void Unload()
    {
        Values.Clear();
        Track.FadeTo(Target0, 0f);
        Release();
    }

    private static void Retarget(float fadeSeconds)
    {
        Target[0] = Values.TryGet("", out float amount) ? amount : 0f;
        Track.FadeTo(Target, fadeSeconds);
        Refresh();
        if (Track.Running)
            FadeTicker.Wake();
    }

    private static void Refresh()
    {
        float shown = Mathf.Clamp01(Track.Current[0]);
        if (shown <= 0f && !Track.Running && Values.IsEmpty)
        {
            Release();
            return;
        }

        var effects = EffectsController.instance;
        if (effects == null)
            return;
        var list = BeautifyList(effects);
        if (list == null)
            return;
        float intensity = shown * MaxIntensity;
        for (int i = 0; i < list.Count; i++)
        {
            var beautify = list[i] != null ? list[i].beautify : null;
            if (beautify == null)
                continue;
            if (!Touched.Contains(beautify))
            {
                if (Touched.Count == 0)
                    _gameWantsBlur = beautify.blur;
                Touched.Add(beautify);
                TouchedIntensity.Add(beautify.blurIntensity);
            }
            beautify.blur = true;
            beautify.blurIntensity = intensity;
        }
    }

    private static void Release()
    {
        for (int i = 0; i < Touched.Count; i++)
        {
            var beautify = Touched[i];
            if (beautify == null)
                continue;
            beautify.blurIntensity = TouchedIntensity[i];
            beautify.blur = _gameWantsBlur;
        }
        ForgetScene();
    }
}
