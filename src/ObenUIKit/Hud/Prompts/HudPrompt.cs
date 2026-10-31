// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>When a prompt shows and what pressing its key does. Every property is optional.</summary>
public sealed class PromptOptions
{
    /// <summary>Show the prompt only while this is true. Asked about five times a second, so keep it cheap.</summary>
    public Func<bool> When { get; set; }

    /// <summary>Show the prompt only while the player is within <see cref="Distance"/> metres of this object.</summary>
    public Transform Near { get; set; }

    /// <summary>How close the player has to be to <see cref="Near"/>.</summary>
    public float Distance { get; set; } = 3f;

    /// <summary>Hide it for good after this many seconds on screen; 0 keeps it until <see cref="HudPrompt.Hide"/>.</summary>
    public float Seconds { get; set; }

    /// <summary>Runs when the player presses the prompt's key while the prompt shows.</summary>
    public Action OnPress { get; set; }

    /// <summary>
    /// With <see cref="OnPress"/>: keep the key from the game while the prompt shows, so pressing E for the
    /// prompt doesn't also interact with whatever is in front of the player. On by default.
    /// </summary>
    public bool Capture { get; set; } = true;

    /// <summary>Hide the prompt after <see cref="OnPress"/> ran once.</summary>
    public bool Once { get; set; }

    /// <summary>Play the tutorial's chime when the prompt appears.</summary>
    public bool Sound { get; set; } = true;

    internal PromptOptions Copy() => (PromptOptions)MemberwiseClone();
}

/// <summary>
/// A "press a key to do something" line in the middle of the screen, drawn in the game's own tutorial
/// prompt (the one that taught moving and interacting) when it's free. Get one from
/// <see cref="Hud.Prompt(ITracker, string, string, PromptOptions)"/>; it goes away with its owner.
/// </summary>
public sealed class HudPrompt
{
    private string _text;

    internal HudPrompt(ITracker owner, string text, PromptKey key, PromptOptions options, int serial)
    {
        Owner = owner;
        _text = text;
        Key = key;
        Options = options;
        Serial = serial;
    }

    internal ITracker Owner { get; }
    internal PromptKey Key { get; }
    internal PromptOptions Options { get; }
    internal int Serial { get; }

    /// <summary>Its conditions held at the last check (asked a few times a second).</summary>
    internal bool Wanted { get; set; } = true;

    /// <summary>Added through <c>Hud.Prompt</c>, so asking again returns it (a guide's own prompts aren't).</summary>
    internal bool Reusable { get; set; }

    /// <summary>Seconds it has been on screen.</summary>
    internal float Shown { get; set; }

    /// <summary>A guide's step listens here for its key.</summary>
    internal Action Pressed { get; set; }

    /// <summary>Bumped when the text changes, so the screen is updated.</summary>
    internal int Version { get; private set; }

    /// <summary>The line shown above the key; <c>{key:Action}</c> works as in hints.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;
            _text = value ?? "";
            Version++;
        }
    }

    /// <summary>It is on screen right now.</summary>
    public bool IsShowing => PromptRuntime.Current != null && PromptRuntime.Current.IsShowing(this);

    /// <summary>It hasn't been hidden or timed out (it may still be waiting for its conditions).</summary>
    public bool IsActive { get; internal set; } = true;

    /// <summary>Takes the prompt away for good.</summary>
    public void Hide() => PromptRuntime.Current?.Remove(this);
}
