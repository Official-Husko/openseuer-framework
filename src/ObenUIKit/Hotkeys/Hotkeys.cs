using System;
using System.Collections.Generic;
using ObenFramework.Features;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using ObenUIKit.Menu;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace ObenUIKit;

/// <summary>
/// Keys that mods register once and the player can rebind on the Hotkeys page of the mod menu.
/// One check per frame covers every hotkey and does nothing until a key goes down. A hotkey only
/// fires where the game itself would accept a key press (see <see cref="HotkeyScope"/>): not while
/// typing, in the console, in a menu or the pause menu.
/// </summary>
public static class Hotkeys
{
    private static readonly List<Hotkey> Registered = new List<Hotkey>();
    private static readonly Func<KeyCode, bool> KeyDown = UInput.GetKeyDown;
    private static bool _anyPressed;
    private static HotkeyStore _store;
    private static ModLogger _log;

    /// <summary>Every registered hotkey, in the order they were added.</summary>
    public static IReadOnlyList<Hotkey> All => Registered;

    /// <summary>
    /// Adds a hotkey for as long as <paramref name="owner"/> (a mod or one of its features) is loaded.
    /// <paramref name="id"/> is kept in hotkeys.json, so it should stay the same between versions.
    /// <paramref name="onPressed"/> runs on the frame the chord goes down; an exception is logged.
    /// </summary>
    public static Hotkey Register(ITracker owner, string id, string label, KeyCode defaultKey, Action onPressed,
        KeyModifiers modifiers = KeyModifiers.None, HotkeyScope scope = HotkeyScope.InGame)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(label))
            throw new ArgumentException("A hotkey needs an id and a label.");
        if (onPressed == null)
            throw new ArgumentNullException(nameof(onPressed));
        if (_store == null)
            throw new InvalidOperationException("ObenUIKit isn't loaded.");

        var mod = ModOf(owner);
        if (Find(mod.Info.Id, id) != null)
            throw new InvalidOperationException($"Hotkey '{id}' is already registered by {mod.Info.Id}.");

        var hotkey = new Hotkey(mod.Info.Id, mod.Info.Manifest.DisplayName, id, label,
            new KeyChord(defaultKey, modifiers), scope, onPressed);
        if (_store.TryGet(hotkey, out var saved))
            hotkey.Load(saved);
        Registered.Add(hotkey);
        owner.Track(Disposable.Create(() =>
        {
            Registered.Remove(hotkey);
            hotkey.Active = false;
            ModMenu.Rebuild();
        }));

        string clash = HotkeyConflicts.Describe(hotkey, Registered);
        if (clash != null)
            _log.Warn($"{hotkey.Chord} for {hotkey.OwnerId}/{id} is a clash: {clash.ToLowerInvariant()}.");
        ModMenu.Rebuild();
        return hotkey;
    }

    /// <summary>The hotkey a mod registered with this id, or null.</summary>
    public static Hotkey Find(string ownerId, string id)
    {
        for (int i = 0; i < Registered.Count; i++)
        {
            if (Registered[i].OwnerId == ownerId && Registered[i].Id == id)
                return Registered[i];
        }
        return null;
    }

    /// <summary>What <paramref name="hotkey"/> shares its key with (another mod's hotkey or a key of the game's), or null.</summary>
    public static string ConflictOf(Hotkey hotkey) => HotkeyConflicts.Describe(hotkey, Registered);

    internal static void Init(ObenMod kit)
    {
        Registered.Clear();
        _log = kit.Log;
        _store = new HotkeyStore(kit.LoadConfig<HotkeyFile>("hotkeys.json"), kit.Log);
        kit.Track(Disposable.Create(() =>
        {
            Registered.Clear();
            _store = null;
        }));
    }

    internal static void Persist(Hotkey hotkey) => _store?.Save(hotkey);

    /// <summary>
    /// The per-frame check. Reads nothing but the keyboard state until a key goes down, and while a hotkey is
    /// pressed, its main key until it comes up.
    /// </summary>
    internal static void Tick()
    {
        bool anyDown = UInput.anyKeyDown;
        if (Registered.Count == 0 || !anyDown && !_anyPressed)
            return;

        bool pressed = false;
        for (int i = 0; i < Registered.Count; i++)
        {
            var hotkey = Registered[i];
            // Forgotten the frame after the main key came up, so Up() still sees it on that frame.
            if (hotkey.WasPressed && !UInput.GetKey(hotkey.Chord.Key) && !UInput.GetKeyUp(hotkey.Chord.Key))
                hotkey.WasPressed = false;
            // The gate is only asked for a hotkey whose key went down this frame.
            if (anyDown && Matches(hotkey, KeyDown) && HotkeyGate.CanFire(hotkey.Scope))
            {
                hotkey.WasPressed = true;
                Fire(hotkey);
            }
            pressed |= hotkey.WasPressed;
        }
        _anyPressed = pressed;
    }

    internal static bool IsDown(Hotkey hotkey) => hotkey.Active && Matches(hotkey, KeyDown) && HotkeyGate.CanFire(hotkey.Scope);

    // Held and up follow the press that went down as this hotkey, whatever the modifiers do after it: letting go
    // of Ctrl before K ends Ctrl+K, it doesn't turn into a plain K.
    internal static bool IsHeld(Hotkey hotkey) =>
        hotkey.Active && UInput.GetKey(hotkey.Chord.Key) && (hotkey.WasPressed || IsDown(hotkey));

    internal static bool IsUp(Hotkey hotkey) => hotkey.Active && hotkey.WasPressed && UInput.GetKeyUp(hotkey.Chord.Key);

    private static bool Matches(Hotkey hotkey, Func<KeyCode, bool> edge)
    {
        var chord = hotkey.Chord;
        if (!chord.IsSet || !edge(chord.Key))
            return false;

        var held = KeyChord.HeldExcluding(chord.Key);
        if (!ChordRules.Matches(true, chord.Modifiers, held))
            return false;

        // Ctrl+K beats plain K when Ctrl is down: the more specific chord wins.
        for (int i = 0; i < Registered.Count; i++)
        {
            var other = Registered[i].Chord;
            if (other.Key == chord.Key && ChordRules.Outranked(chord.Modifiers, other.Modifiers, held))
                return false;
        }
        return true;
    }

    private static void Fire(Hotkey hotkey)
    {
        try
        {
            hotkey.Pressed();
        }
        catch (Exception e)
        {
            _log.Error($"Hotkey '{hotkey.Id}' of {hotkey.OwnerId} threw", e);
        }
    }

    private static ObenMod ModOf(ITracker owner)
    {
        if (owner is ObenMod mod)
            return mod;
        if (owner is ModFeature feature && feature.Owner != null)
            return feature.Owner;
        throw new ArgumentException("The owner of a hotkey must be a mod or one of its features.", nameof(owner));
    }
}
