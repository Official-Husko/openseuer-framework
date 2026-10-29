// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
// The match and overlap rules come from its KeyChord; the cases its KeyChordSelfTest checked at start are unit tests here.
using System;

namespace ObenUIKit;

/// <summary>The modifier keys a hotkey can need held. Left and right keys count the same.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

/// <summary>When a chord counts as pressed, once the keys are read. No Unity types, so it is unit tested.</summary>
internal static class ChordRules
{
    /// <summary>
    /// The main key gave its edge (down, up or held) and every modifier the chord needs is held. Extra held
    /// modifiers don't stop it.
    /// </summary>
    public static bool Matches(bool mainKey, KeyModifiers needed, KeyModifiers held) => mainKey && (needed & ~held) == 0;

    /// <summary>
    /// Another chord on the same main key needs all of <paramref name="mine"/> and more, and all of it is held:
    /// that one wins, so Ctrl+K doesn't also fire plain K.
    /// </summary>
    public static bool Outranked(KeyModifiers mine, KeyModifiers other, KeyModifiers held) =>
        other != mine && (other & mine) == mine && (other & ~held) == 0;

    /// <summary>Pressing one also presses the other: the modifiers of one are a subset of the other's.</summary>
    public static bool Overlap(KeyModifiers a, KeyModifiers b)
    {
        var shared = a & b;
        return shared == a || shared == b;
    }
}
