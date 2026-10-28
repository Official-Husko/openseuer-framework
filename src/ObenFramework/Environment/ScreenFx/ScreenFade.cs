using System.Collections.Generic;
using HarmonyLib;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// Black screen holds on the game's own fade canvas. The screen goes black when the first mod asks and
/// comes back when the last one lets go. A level change drops every hold (the game fades the new level
/// in itself), and so does a mod unloading.
/// </summary>
internal static class ScreenFade
{
    private static readonly OwnedValues<float> Holds = new OwnedValues<float> { Released = _ => ReleasedByUnload() };

    public static bool Any => !Holds.IsEmpty;

    public static List<OwnedValue<float>> Entries => Holds.Entries;

    public static bool ToBlack(ObenMod owner, float speed)
    {
        var canvas = BlackCanvas.instance;
        if (canvas == null || canvas.fadeCanvasGroup == null)
            return false;
        Holds.Set(owner, owner.Info.Id, "", speed);
        canvas.FadeIn(speed);
        return true;
    }

    public static bool FromBlack(ObenMod owner, float speed)
    {
        if (!Holds.Remove(owner, ""))
            return false;
        if (Holds.IsEmpty)
            FadeOut(speed);
        return true;
    }

    /// <summary>The level is changing: forget the holds without touching the canvas, the game is fading itself.</summary>
    public static void ForgetScene() => Holds.Clear();

    public static void Unload()
    {
        bool held = Any;
        Holds.Clear();
        if (held)
            FadeOut(0.1f);
    }

    private static void ReleasedByUnload()
    {
        if (Holds.IsEmpty)
            FadeOut(0.1f);
    }

    // BlackCanvas.FadeOut would also give the player's controls back, which may be held by someone else.
    private static void FadeOut(float speed)
    {
        var canvas = BlackCanvas.instance;
        if (canvas != null && canvas.fadeCanvasGroup != null)
            canvas.fadeCanvasGroup.StartFadeOut(speed);
    }
}

/// <summary>Calls the game's camera shake without its drift: a shake cut short by another would start from the offset position.</summary>
internal static class ScreenShake
{
    private static readonly AccessTools.FieldRef<Coroutine> Running = AccessTools.StaticFieldRefAccess<Coroutine>(AccessTools.Field(typeof(CameraShake), "shakeCo"));
    private static readonly AccessTools.FieldRef<CameraShake, Vector3> Origin = AccessTools.FieldRefAccess<CameraShake, Vector3>("originalPos");

    public static bool Shake(float seconds, float amount)
    {
        var shake = CameraShake.instance;
        if (shake == null)
            return false;
        if (Running() != null)
            shake.transform.localPosition = Origin(shake);
        CameraShake.Shake(seconds, amount);
        return true;
    }
}
