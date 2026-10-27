using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>How much the player lost in one hit and what caused it.</summary>
public readonly struct PlayerDamage
{
    public PlayerDamage(float amount, DamageTypeNames reason)
    {
        Amount = amount;
        Reason = reason;
    }

    /// <summary>Health actually lost (bleeding gained for <see cref="DamageTypeNames.Bleed"/>), after protection and clamping.</summary>
    public float Amount { get; }

    public DamageTypeNames Reason { get; }
}

/// <summary>
/// Things that happen to the player. Subscribe with an owner (<c>PlayerEvents.Died.Subscribe(this, ...)</c>)
/// so a hot reload removes the handler; each handler runs in its own try/catch.
/// </summary>
public static class PlayerEvents
{
    /// <summary>The death screen was triggered (once per death; god mode blocks it). The payload is the cause.</summary>
    public static readonly GameEvent<DamageTypeNames> Died = new GameEvent<DamageTypeNames>(nameof(Died));

    /// <summary>The player lost health or gained bleeding through the game's damage call. Healing does not raise it.</summary>
    public static readonly GameEvent<PlayerDamage> Damaged = new GameEvent<PlayerDamage>(nameof(Damaged));
}
