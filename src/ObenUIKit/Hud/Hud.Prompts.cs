// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>Key prompts and guides in the game's tutorial prompt.</summary>
public static partial class Hud
{
    /// <summary>
    /// Shows "text" over the key of one of the game's actions ("Interact", "Jump", "Inventory"...) in the
    /// middle of the screen, the way the game's tutorial does it, following the player's key bindings and
    /// controller. Tie it to a place or a condition with <paramref name="options"/>, and give it an
    /// <see cref="PromptOptions.OnPress"/> to act on the key. Only one prompt shows at a time: the newest
    /// one whose conditions hold. It goes away when <paramref name="owner"/> unloads or with <see cref="HudPrompt.Hide"/>.
    /// </summary>
    public static HudPrompt Prompt(ITracker owner, string text, string action, PromptOptions options = null)
    {
        if (string.IsNullOrEmpty(action))
            throw new ArgumentException("Name the game action the prompt shows.", nameof(action));
        return AddPrompt(owner, text, PromptKey.ForAction(action), options);
    }

    /// <summary>Shows "text" over a fixed key (a mod's own key).</summary>
    public static HudPrompt Prompt(ITracker owner, string text, KeyCode key, PromptOptions options = null)
    {
        if (key == KeyCode.None)
            throw new ArgumentException("Pick the key the prompt shows.", nameof(key));
        return AddPrompt(owner, text, PromptKey.ForKey(key), options);
    }

    /// <summary>
    /// Shows "text" over a mod hotkey's key (with its Ctrl, Shift or Alt), following the player's rebinding.
    /// The hotkey's own action already runs on its key; <see cref="PromptOptions.OnPress"/> is for extras.
    /// </summary>
    public static HudPrompt Prompt(ITracker owner, string text, Hotkey hotkey, PromptOptions options = null)
    {
        if (hotkey == null)
            throw new ArgumentNullException(nameof(hotkey));
        return AddPrompt(owner, text, PromptKey.ForHotkey(hotkey), options);
    }

    /// <summary>The prompt on screen right now, or null.</summary>
    public static HudPrompt ShownPrompt => PromptRuntime.Current?.Shown;

    /// <summary>
    /// A new, empty guide: a walk-through of several prompts that each wait for their key or a condition.
    /// Add steps with <see cref="HudGuide.Step(string, string, Func{bool})"/> and call <see cref="HudGuide.Start"/>.
    /// Starting a guide stops a running one with the same id.
    /// </summary>
    public static HudGuide Guide(ITracker owner, string id)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("A guide needs an id.", nameof(id));
        return new HudGuide(owner, id);
    }

    private static HudPrompt AddPrompt(ITracker owner, string text, PromptKey key, PromptOptions options)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("A prompt needs some text.", nameof(text));
        var runtime = PromptRuntime.Current ?? throw new InvalidOperationException("ObenUIKit isn't loaded.");
        var copy = options != null ? options.Copy() : new PromptOptions();
        if (copy.Distance <= 0f)
            copy.Distance = 3f;
        return runtime.Add(owner, text, key, copy);
    }
}
