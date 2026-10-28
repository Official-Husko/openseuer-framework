// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;

namespace ObenFramework.Player.Look;

/// <summary>
/// The player's look: read it as a <see cref="LookData"/>, dress any Appearance with one, and hear about
/// changes. Worn items travel by asset name, and applying never writes into the game's shared
/// <c>Character</c> assets (see <see cref="Apply"/>). Safe at the main menu: reads return null or false.
/// </summary>
public static partial class PlayerLook
{
    /// <summary>
    /// The player's look changed: the character editor moved a slider or picked an item, or
    /// <see cref="Apply"/> dressed the player. Also fires when the player's body is built after a level
    /// load if it differs from the last look announced. Edits made within a fraction of a second arrive
    /// as one event; handlers run on the main thread in their own try/catch.
    /// </summary>
    public static readonly GameEvent<LookData> Changed = new GameEvent<LookData>(nameof(Changed));

    /// <summary>The player's body exists and can be read or dressed.</summary>
    public static bool IsAvailable => PlayerAppearance != null;

    /// <summary>
    /// The Appearance of the player's third-person body, or null at the main menu. It is not always
    /// <c>PlayerLocator.playerAppearance</c> (the game leaves that empty in some scenes), so this looks
    /// through the player's children, skipping the first-person arms. Cached per level.
    /// </summary>
    public static Appearance PlayerAppearance => PlayerAppearances.Resolve();

    /// <summary>Describes the local player's look, or null when there is no player.</summary>
    public static LookData Capture()
    {
        var appearance = PlayerAppearance;
        return appearance != null ? Capture(appearance) : null;
    }

    /// <summary>
    /// Describes any Appearance from its live values (not from its Character asset, which for the player
    /// can be the shared "Random NPC" one). The head is "" when the Appearance has none selected.
    /// </summary>
    public static LookData Capture(Appearance appearance)
    {
        if (appearance == null)
            throw new ArgumentNullException(nameof(appearance));

        var look = new LookData { male = appearance.male, advanced = appearance.advanced };
        foreach (var dial in LookDials.All)
            dial.Set(look, dial.Clamp(dial.Read(appearance)));
        foreach (var slot in LookSlots.All)
            slot.Set(look, NameOf(slot.Selected(appearance)));
        return look;
    }

    /// <summary>The names an item slot accepts ("head", "hat", "hair", "glasses", "beard", "jacket", "shirt", "pants", "leggings", "socks", "shoes").</summary>
    public static IReadOnlyList<string> SlotNames
    {
        get
        {
            var names = new string[LookSlots.All.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = LookSlots.All[i].Name;
            return names;
        }
    }

    /// <summary>
    /// The asset names a slot can wear, for validating a look received from elsewhere. "-" is the
    /// empty item. Heads depend on <paramref name="male"/>; other slots ignore it.
    /// </summary>
    public static IReadOnlyCollection<string> Options(string slot, bool male)
    {
        foreach (var candidate in LookSlots.All)
        {
            if (string.Equals(candidate.Name, slot, StringComparison.OrdinalIgnoreCase))
                return WearableTable.Names(candidate, male);
        }
        throw new ArgumentException($"Unknown look slot '{slot}'. Known: {string.Join(", ", SlotNames)}");
    }

    internal static string NameOf(Wearable wearable) =>
        wearable == null || wearable.name == "-" ? "" : wearable.name;
}
