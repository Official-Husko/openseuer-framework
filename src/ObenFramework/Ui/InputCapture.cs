// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Patching;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Ui;

/// <summary>
/// Keeps keys away from the game while a mod wants them. The game reads its actions ("Interact", "Jump",
/// "Inventory"...) through its <c>InputManager</c>; a capture makes those reads come back as not pressed,
/// so a mod can use E for its own prompt without the player also interacting, or keep the game still while
/// a mod's panel is up. The mod itself still sees the key through <c>UnityEngine.Input</c>.
/// </summary>
/// <remarks>
/// The keys the game reads straight from Unity are held back too, for the game only (<see cref="RawKeys"/>): F1
/// (console), F11 and F12 (screenshots) and pad buttons only by <see cref="Key"/> (F11 and F12 also by their
/// "Screenshot" and "screenshotNoUI" actions); 1 to 4 (equipment) and the interact list's X and C also by
/// <see cref="All"/> and while typing. Inside the game's own menus some keys stay out of reach. While a mod's
/// text field has the keyboard, the game's actions are held back (except the pause menu, menu, exit and
/// interact keys), so typing doesn't walk the player around; turn that off with <see cref="WhileTyping"/>.
/// </remarks>
public static class InputCapture
{
    private enum Kind
    {
        All,
        Action,
        Key,
    }

    private sealed class Capture
    {
        public Kind Kind;
        public string Action;
        public KeyCode Key;
    }

    // The game's own typing fields and menus still need these while a mod's text field is focused.
    private static readonly string[] KeptWhileTyping = { "Pause Menu", "Menu", "Exit Cancel", "Interact" };

    private static readonly List<Capture> Captures = new List<Capture>();
    private static int _all;
    private static int _typingFrame = -1;
    private static bool _typing;
    private static bool _typingFailed;

    /// <summary>A capture started or the last one ended. The payload is <see cref="Any"/>.</summary>
    public static readonly GameEvent<bool> Changed = new GameEvent<bool>(nameof(Changed));

    /// <summary>Hold back the game's actions while a text field has the keyboard (on by default).</summary>
    public static bool WhileTyping { get; set; } = true;

    /// <summary>Some mod is capturing input right now.</summary>
    public static bool Any => Captures.Count > 0;

    /// <summary>Every game action is held back.</summary>
    public static bool AllCaptured => _all > 0;

    /// <summary>The hooks on the game's key reads are in place (false when a game update broke them).</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Ui");

    /// <summary>The hooks on the keys the game reads straight from Unity are in place.</summary>
    public static bool RawKeysAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "RawKeys");

    /// <summary>
    /// Holds back one of the game's actions (the names its key bindings use: "Interact", "Jump", "Inventory",
    /// "Pause Menu") until the result is disposed or <paramref name="owner"/> unloads.
    /// </summary>
    public static IDisposable Action(ITracker owner, string action)
    {
        if (string.IsNullOrEmpty(action))
            throw new ArgumentException("Name the game action to capture.", nameof(action));
        return Add(owner, new Capture { Kind = Kind.Action, Action = action });
    }

    /// <summary>Holds back every game action bound to <paramref name="key"/> (on the keyboard or the pad).</summary>
    public static IDisposable Key(ITracker owner, KeyCode key)
    {
        if (key == KeyCode.None)
            throw new ArgumentException("Pick a key to capture.", nameof(key));
        return Add(owner, new Capture { Kind = Kind.Key, Key = key });
    }

    /// <summary>Holds back every game action: the player can't move, look around with keys, open menus or use things.</summary>
    public static IDisposable All(ITracker owner) => Add(owner, new Capture { Kind = Kind.All });

    /// <summary>True when the game's <paramref name="action"/> is held back right now (by name; key captures are checked against its binding).</summary>
    public static bool IsCaptured(string action)
    {
        var input = InputManager.instance;
        return input != null && Swallows(input, action);
    }

    /// <summary>Called by the patches on InputManager when one of its key reads came back true.</summary>
    internal static bool Swallows(InputManager input, string action)
    {
        if (_all > 0)
            return true;
        for (int i = 0; i < Captures.Count; i++)
        {
            var capture = Captures[i];
            if (capture.Kind == Kind.Action && string.Equals(capture.Action, action, StringComparison.OrdinalIgnoreCase))
                return true;
            if (capture.Kind == Kind.Key && BoundTo(input, action, capture.Key))
                return true;
        }
        return WhileTyping && input.inputType != InputManager.InputType.Controller && Typing() && !Kept(action);
    }

    /// <summary>Called through <see cref="RawKeys"/> when one of the game's direct key reads came back true.</summary>
    internal static bool HoldsRaw(KeyCode key)
    {
        // F-keys and pad buttons are the console, screenshots and menus: only a capture of that very key holds them.
        bool system = key >= KeyCode.F1 && key <= KeyCode.F15 || key >= KeyCode.JoystickButton0;
        if (_all > 0 && !system)
            return true;
        string action = ActionOf(key);
        for (int i = 0; i < Captures.Count; i++)
        {
            var capture = Captures[i];
            if (capture.Kind == Kind.Key && capture.Key == key)
                return true;
            if (capture.Kind == Kind.Action && action != null && string.Equals(capture.Action, action, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return !system && WhileTyping && Typing();
    }

    // The game reads F11 and F12 both through these actions and straight from Unity.
    private static string ActionOf(KeyCode key) =>
        key == KeyCode.F11 ? "Screenshot" : key == KeyCode.F12 ? "screenshotNoUI" : null;

    internal static void Init(ObenLoader.ObenMod framework)
    {
        Captures.Clear();
        _all = 0;
        _typingFailed = false;
        WhileTyping = true;
        framework.Track(Disposable.Create(() =>
        {
            Captures.Clear();
            _all = 0;
        }));
    }

    private static IDisposable Add(ITracker owner, Capture capture)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        bool wasAny = Any;
        Captures.Add(capture);
        if (capture.Kind == Kind.All)
            _all++;
        if (!wasAny)
            Changed.Raise(true, GameEvents.Log);

        var registration = Disposable.Create(() => Remove(capture));
        owner.Track(registration);
        return registration;
    }

    private static void Remove(Capture capture)
    {
        if (!Captures.Remove(capture))
            return;
        if (capture.Kind == Kind.All)
            _all--;
        if (!Any)
            Changed.Raise(false, GameEvents.Log);
    }

    private static bool BoundTo(InputManager input, string action, KeyCode key)
    {
        if (input.GetKeyCode(action, ignoreController: true) == key)
            return true;
        return input.inputType == InputManager.InputType.Controller && input.GetKeyCode(action) == key;
    }

    private static bool Kept(string action)
    {
        for (int i = 0; i < KeptWhileTyping.Length; i++)
        {
            if (string.Equals(KeptWhileTyping[i], action, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Asked once per frame at most: held keys (walking) read the same actions every frame. A failing check
    // counts as not typing and is logged once, so the captures themselves keep working.
    private static bool Typing()
    {
        int frame = Time.frameCount;
        if (frame == _typingFrame)
            return _typing;
        _typingFrame = frame;
        try
        {
            _typing = UiState.IsTyping;
        }
        catch (Exception e)
        {
            _typing = false;
            if (!_typingFailed)
            {
                _typingFailed = true;
                GameEvents.Log.Error("Checking whether the player is typing failed; captures go on without it", e);
            }
        }
        return _typing;
    }
}
