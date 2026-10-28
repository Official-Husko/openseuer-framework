// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Events;
using ObenLoader;

namespace ObenFramework.Player.Survival;

/// <summary>
/// Things that happen to the player's body, to complement <see cref="PlayerEvents.Died"/> and
/// <see cref="PlayerEvents.Damaged"/>. Subscribe with an owner so a hot reload removes the handler.
/// </summary>
public static class SurvivalEvents
{
    /// <summary>The player passed out from exhaustion or drink (not a bed or a chosen sleep). Fires as it starts.</summary>
    public static readonly GameEvent PassedOut = new GameEvent(nameof(PassedOut));

    /// <summary>The player used up a food, drink or other consumable, after its effects applied.</summary>
    public static readonly GameEvent<ConsumedItem> Consumed = new GameEvent<ConsumedItem>(nameof(Consumed));

    /// <summary>Sets up the survival hooks that keep state between calls. Their Harmony patches are applied by the loader.</summary>
    internal static void Init(ObenMod framework)
    {
        StatThresholds.Init(framework);
        StatDrain.Init(framework);
    }
}
