using System;

namespace ObenFramework.Npcs;

/// <summary>
/// Parts of an NPC's own behaviour a claim can switch off while it holds the NPC (see
/// <see cref="NpcClaim.Hold"/>). Everything is switched back on when the claim ends.
/// </summary>
[Flags]
public enum NpcHolds
{
    None = 0,

    /// <summary>The NPC does not notice the player or anything else through its field of view (no crimes seen, no police reaction).</summary>
    Sight = 1,

    /// <summary>The player cannot start a conversation with the NPC.</summary>
    Talk = 2,

    /// <summary>The NPC makes no barks or comments of its own; <see cref="NpcActions.Say"/> and <see cref="NpcActions.Comment"/> are refused too.</summary>
    Barks = 4,

    /// <summary>The NPC stays shown at any distance and is not culled when out of sight, so a scene you stage stays put.</summary>
    Visible = 8,

    /// <summary>The NPC cannot move at all (its path finding agent is stopped) and its activity is paused.</summary>
    Freeze = 16,

    All = Sight | Talk | Barks | Visible | Freeze,
}
