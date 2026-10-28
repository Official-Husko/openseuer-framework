using System;

namespace ObenUIKit;

/// <summary>Where a hotkey may fire. Typing, the console and the pause menu always block it.</summary>
[Flags]
public enum HotkeyScope
{
    /// <summary>In a level, with no menu, dialogue, shop or kit window open.</summary>
    InGame = 1,
    /// <summary>At the main menu.</summary>
    MainMenu = 2,
    /// <summary>While a kit window is open (a hotkey that closes its own window, for example).</summary>
    Windows = 4,
    Anywhere = InGame | MainMenu | Windows,
}

/// <summary>
/// A mod's hotkey. The player can rebind it on the Hotkeys page of the mod menu; the choice is kept
/// in the kit's hotkeys.json. It goes away when the mod that registered it unloads.
/// </summary>
public sealed class Hotkey
{
    private KeyChord _chord;

    internal Hotkey(string ownerId, string ownerName, string id, string label, KeyChord defaultChord,
        HotkeyScope scope, Action pressed)
    {
        OwnerId = ownerId;
        OwnerName = ownerName;
        Id = id;
        Label = label;
        Default = defaultChord;
        Scope = scope;
        Pressed = pressed;
        _chord = defaultChord;
    }

    public string OwnerId { get; }

    /// <summary>The owning mod's display name, as the Hotkeys page groups it.</summary>
    public string OwnerName { get; }

    public string Id { get; }

    public string Label { get; }

    public KeyChord Default { get; }

    public HotkeyScope Scope { get; }

    /// <summary>The chord in use now: the player's choice, or the default.</summary>
    public KeyChord Chord => _chord;

    public bool IsDefault => _chord == Default;

    /// <summary>Raised after the chord changed, by the player or by <see cref="Rebind"/>.</summary>
    public event Action<KeyChord> Changed;

    internal Action Pressed { get; }

    internal string StoreKey => OwnerId + "/" + Id;

    /// <summary>Changes the chord and keeps it for the next session. A chord that isn't set turns the hotkey off.</summary>
    public void Rebind(KeyChord chord)
    {
        if (chord == _chord)
            return;
        _chord = chord;
        Hotkeys.Persist(this);
        Changed?.Invoke(chord);
    }

    /// <summary>Goes back to the default chord.</summary>
    public void Reset() => Rebind(Default);

    /// <summary>Sets the chord read from the file, without saving or raising <see cref="Changed"/>.</summary>
    internal void Load(KeyChord chord) => _chord = chord;
}
