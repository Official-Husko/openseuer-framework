using System;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// Full-screen effects: camera shake, fade to black, a coloured vignette and a blur. The lasting ones are
/// held per mod: they stay while the mod wants them, the newest mod's values show when several hold one,
/// and they go away when the mod clears them or unloads. Colours are given the usual way (sRGB, like a
/// colour picker); the game renders in linear colour space and they are converted for it. Shakes, fades and
/// the death message are refused (false) while a save is written or loaded, when the game runs the screen.
/// </summary>
public static class ScreenFx
{
    /// <summary>True when the hooks are in place that keep the game's own menu blur under a mod's blur.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    /// <summary>True while some mod holds the screen black.</summary>
    public static bool IsHeldBlack => ScreenFade.Any;

    /// <summary>True while some mod holds a vignette.</summary>
    public static bool HasVignette => ScreenVignette.Any;

    /// <summary>True while some mod holds a blur.</summary>
    public static bool HasBlur => ScreenBlur.Any;

    /// <summary>
    /// Shakes the camera for <paramref name="seconds"/> (real time) by up to <paramref name="amount"/> metres,
    /// like an explosion nearby. Returns false when there is no camera to shake (main menu, loading).
    /// </summary>
    public static bool Shake(float seconds, float amount)
    {
        if (!(seconds > 0f) || float.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds), "Give a positive number of seconds.");
        if (!(amount >= 0f) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount), "Give an amount of 0 or more.");
        return !SaveGate.IsBusy && ScreenShake.Shake(seconds, amount);
    }

    /// <summary>
    /// Fades the screen to black and holds it until <paramref name="owner"/> calls <see cref="FadeFromBlack"/>,
    /// unloads, or the level changes. <paramref name="speed"/> is the game's fade step: 0.1 takes about a
    /// second, 1 is almost instant. Returns false outside a gameplay scene.
    /// </summary>
    public static bool FadeToBlack(ObenMod owner, float speed = 0.1f)
    {
        RequireOwner(owner);
        RequireSpeed(speed);
        return !SaveGate.IsBusy && ScreenFade.ToBlack(owner, speed);
    }

    /// <summary>
    /// Ends <paramref name="owner"/>'s hold; the screen fades back once no mod holds it. True when it had one.
    /// While a save is written or loaded the hold stays (false), so the game's own black screen isn't lifted.
    /// </summary>
    public static bool FadeFromBlack(ObenMod owner, float speed = 0.1f)
    {
        RequireOwner(owner);
        RequireSpeed(speed);
        return !SaveGate.IsBusy && ScreenFade.FromBlack(owner, speed);
    }

    /// <summary>
    /// Darkens or tints the screen edges. <paramref name="strength"/> 0 to 1 sets how far in it reaches; the
    /// colour's alpha how opaque it gets. Fades over <paramref name="fadeSeconds"/> (real time).
    /// </summary>
    public static void Vignette(ObenMod owner, Color color, float strength, float fadeSeconds = 0f)
    {
        RequireOwner(owner);
        RequireFinite(color.r, nameof(color));
        RequireFinite(color.g, nameof(color));
        RequireFinite(color.b, nameof(color));
        RequireFinite(color.a, nameof(color));
        RequireFinite(strength, nameof(strength));
        RequireFade(fadeSeconds);
        var linear = color.linear;
        linear.a = Mathf.Clamp01(color.a);
        ScreenVignette.Set(owner, linear, Mathf.Clamp01(strength), fadeSeconds);
    }

    /// <summary>Removes <paramref name="owner"/>'s vignette, fading over <paramref name="fadeSeconds"/>. True when it had one.</summary>
    public static bool ClearVignette(ObenMod owner, float fadeSeconds = 0f)
    {
        RequireOwner(owner);
        RequireFade(fadeSeconds);
        return ScreenVignette.Clear(owner, fadeSeconds);
    }

    /// <summary>Blurs the whole screen; <paramref name="amount"/> 0 to 1. Fades over <paramref name="fadeSeconds"/> (real time).</summary>
    public static void Blur(ObenMod owner, float amount, float fadeSeconds = 0f)
    {
        RequireOwner(owner);
        RequireFinite(amount, nameof(amount));
        RequireFade(fadeSeconds);
        ScreenBlur.Set(owner, Mathf.Clamp01(amount), fadeSeconds);
    }

    /// <summary>Removes <paramref name="owner"/>'s blur, fading over <paramref name="fadeSeconds"/>. True when it had one.</summary>
    public static bool ClearBlur(ObenMod owner, float fadeSeconds = 0f)
    {
        RequireOwner(owner);
        RequireFade(fadeSeconds);
        return ScreenBlur.Clear(owner, fadeSeconds);
    }

    /// <summary>Sets the line the death screen shows under its title. Returns false outside a gameplay scene or during a save.</summary>
    public static bool SetDeathMessage(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        var message = DeathMessage.instance;
        if (message == null || SaveGate.IsBusy)
            return false;
        message.ChangeDeathMessage(text);
        return true;
    }

    private static void RequireOwner(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
    }

    private static void RequireSpeed(float speed)
    {
        if (!(speed > 0f) || float.IsInfinity(speed))
            throw new ArgumentOutOfRangeException(nameof(speed), "Give a positive fade speed.");
    }

    private static void RequireFade(float seconds)
    {
        if (!(seconds >= 0f) || float.IsInfinity(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds), "Give 0 or more seconds.");
    }

    private static void RequireFinite(float value, string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentException("The value must be a finite number.", name);
    }
}
