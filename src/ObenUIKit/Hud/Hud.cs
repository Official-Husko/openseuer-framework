using System;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// Hints, subtitles and small bars on the game's HUD. Everything belongs to the mod (or feature)
/// that adds it and goes when that unloads. Nothing shows while a kit window or the pause menu is
/// open, while a save loads, or while the game's own HUD is hidden; timers wait until it is back.
/// </summary>
public static partial class Hud
{
    public const float DefaultHintSeconds = 6f;
    public const float DefaultSubtitleSeconds = 4f;

    /// <summary>
    /// Shows a hint for <paramref name="seconds"/> in the game's own hint panel (the side panel its
    /// scenes use), so it looks native. <c>{key:Interact}</c> in the text turns into the player's
    /// current key for that action. Hints show one at a time in the order they were added; asking for
    /// the same text again extends the one already waiting. Outside levels, where the game has no
    /// hint panel, the kit draws an equivalent one.
    /// </summary>
    /// <param name="owner">The mod or feature the hint belongs to.</param>
    /// <param name="text">The hint, which may have several lines.</param>
    /// <param name="seconds">How long it shows once it is up, from 0.5 to 120.</param>
    /// <param name="title">The panel's title; the mod's name when left out.</param>
    /// <param name="force">
    /// Show it even if the player switched the game's hints off with the Hints key, or mod hints off in the
    /// UI kit's settings. Keep it for hints the player can't do without.
    /// </param>
    /// <returns>The hint; one the player switched off comes back with <see cref="HudHint.IsActive"/> false.</returns>
    public static HudHint Hint(ITracker owner, string text, float seconds = DefaultHintSeconds,
        string title = null, bool force = false)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("A hint needs some text.", nameof(text));
        title = title ?? HudRuntime.NameOf(owner);
        seconds = Seconds(seconds, DefaultHintSeconds);
        if (!force && !ModHintsShown)
            return new HudHint(owner, title, text, seconds, force) { IsActive = false };
        return Live.AddHint(owner, title, text, seconds, force);
    }

    /// <summary>The hint on screen right now, or null.</summary>
    public static HudHint ShownHint => HudRuntime.Current?.ShownHint;

    /// <summary>Takes every hint of <paramref name="owner"/> away, showing or waiting.</summary>
    public static void HideHints(ITracker owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        HudRuntime.Current?.RemoveHints(owner);
    }

    /// <summary>The player lets mod hints show (the UI kit's "Show mod hints" setting). Switched off, waiting hints that aren't forced go.</summary>
    public static bool ModHintsShown => UIKitMod.Settings.ShowModHints;

    /// <summary>The player lets mod key prompts show (the UI kit's "Show mod key prompts" setting).</summary>
    public static bool ModPromptsShown => UIKitMod.Settings.ShowModPrompts;

    /// <summary>
    /// Shows one line of text (rich text) at the bottom of the screen for <paramref name="seconds"/> and
    /// then fades it. A new line replaces the one showing. Works at the main menu too.
    /// </summary>
    public static void Subtitle(string text, float seconds = DefaultSubtitleSeconds)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("A subtitle needs some text.", nameof(text));
        Live.ShowSubtitle(text, Seconds(seconds, DefaultSubtitleSeconds));
    }

    /// <summary>
    /// Adds a line to the game's own subtitles, where the characters' remarks show: it types out at
    /// <paramref name="charactersPerSecond"/>, then fades over <paramref name="fadeSeconds"/>. Lines stack
    /// under each other and can't be taken back. False outside levels, where the game has no subtitles.
    /// </summary>
    public static bool GameSubtitle(string text, float fadeSeconds = DefaultSubtitleSeconds, float charactersPerSecond = 20f)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("A subtitle needs some text.", nameof(text));
        var subtitles = Subtitles.instance;
        if (subtitles == null)
            return false;
        subtitles.ShowLine(text, Seconds(fadeSeconds, DefaultSubtitleSeconds), charactersPerSecond > 0f ? charactersPerSecond : 20f);
        return true;
    }

    /// <summary>
    /// Adds a small bar at the top left that follows <paramref name="value"/> (0 empty, 1 full). The
    /// function is read about five times a second, so keep it cheap; if it throws, the bar is removed.
    /// Returns null when six bars are already up.
    /// </summary>
    public static HudBar Bar(ITracker owner, string label, Func<float> value)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        return Live.AddBar(owner, label ?? "", value);
    }

    private static HudRuntime Live =>
        HudRuntime.Current ?? throw new InvalidOperationException("ObenUIKit isn't loaded.");

    private static float Seconds(float seconds, float fallback) =>
        float.IsNaN(seconds) ? fallback : Mathf.Clamp(seconds, 0.5f, 120f);
}
