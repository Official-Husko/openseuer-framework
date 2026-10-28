using System;
using ObenFramework.Ui;
using ObenLoader.Runtime;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace ObenUIKit;

/// <summary>
/// The key a prompt asks for: one of the game's actions (shown and read through the player's bindings),
/// a fixed key, or a mod hotkey (follows the player's rebinding).
/// </summary>
internal sealed class PromptKey
{
    private static readonly ITracker Untracked = new UntrackedScope();

    private PromptKey(string action, KeyCode key, Hotkey hotkey)
    {
        Action = action;
        Key = key;
        Hotkey = hotkey;
    }

    public string Action { get; }
    public KeyCode Key { get; }
    public Hotkey Hotkey { get; }

    public static PromptKey ForAction(string action) => new PromptKey(action, KeyCode.None, null);

    public static PromptKey ForKey(KeyCode key) => new PromptKey(null, key, null);

    public static PromptKey ForHotkey(Hotkey hotkey) => new PromptKey(null, KeyCode.None, hotkey);

    public static PromptKey None() => new PromptKey(null, KeyCode.None, null);

    public bool IsNone => Action == null && Key == KeyCode.None && Hotkey == null;

    /// <summary>The key right now: the binding of the action, the fixed key, or the hotkey's chord key.</summary>
    public KeyCode Current
    {
        get
        {
            if (Hotkey != null)
                return Hotkey.Chord.Key;
            if (Action == null)
                return Key;
            var input = InputManager.instance;
            return input != null ? input.GetKeyCode(Action) : KeyCode.None;
        }
    }

    /// <summary>What the key looks like on screen ("E", "SPACE", "Ctrl+K").</summary>
    public string Display()
    {
        if (Hotkey != null)
            return Hotkey.Chord.IsSet ? Hotkey.Chord.ToString() : "Not set";
        var input = InputManager.instance;
        if (Action != null)
            return input != null ? input.GetKeyCodeDisplayText(Action) : Action;
        return input != null ? input.GetKeyCodeDisplayText(Key) : Key.ToString();
    }

    /// <summary>
    /// The key went down this frame, read from Unity directly so a capture of the same key (which only
    /// changes the game's answers) doesn't hide it from us.
    /// </summary>
    public bool Pressed()
    {
        if (Hotkey != null)
            return Hotkey.Chord.IsSet && UInput.GetKeyDown(Hotkey.Chord.Key) && (KeyChord.Held() & Hotkey.Chord.Modifiers) == Hotkey.Chord.Modifiers;
        if (Action == null)
            return Key != KeyCode.None && UInput.GetKeyDown(Key);
        var input = InputManager.instance;
        if (input == null)
            return false;
        var keyboard = input.GetKeyCode(Action, ignoreController: true);
        if (keyboard != KeyCode.None && UInput.GetKeyDown(keyboard))
            return true;
        var current = input.GetKeyCode(Action);
        return current != keyboard && current != KeyCode.None && UInput.GetKeyDown(current);
    }

    /// <summary>Keeps the key from the game; the caller disposes the result.</summary>
    public IDisposable Capture()
    {
        if (Action != null)
            return InputCapture.Action(Untracked, Action);
        var key = Current;
        return key != KeyCode.None ? InputCapture.Key(Untracked, key) : null;
    }

    // Captures made here are disposed by the prompt itself, so nothing needs to remember them.
    private sealed class UntrackedScope : ITracker
    {
        public T Track<T>(T disposable) where T : IDisposable => disposable;
    }
}
