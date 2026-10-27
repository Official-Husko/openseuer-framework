using System;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Healing, damage and death through the game's own entry points, so hurt effects, camera shake,
/// god mode and the death screen behave as they do for the game itself.
/// </summary>
public static class PlayerVitals
{
    /// <summary>
    /// Hurts the player through <c>PlayerStatsHealth.Damage</c>, the call falls, explosions and NPCs
    /// use. Dying at 0 health honours god mode. A negative amount heals.
    /// </summary>
    /// <param name="silent">Skip the hurt sound, flash and camera shake.</param>
    public static void Damage(float amount, DamageTypeNames reason = DamageTypeNames.Unspecified, bool silent = false) =>
        Stats().health.Damage(amount, reason, silent);

    /// <summary>Restores health right now, capped at 100.</summary>
    public static void Heal(float amount)
    {
        var stats = Stats();
        stats.health.currentStatus = Mathf.Clamp(stats.health.currentStatus + amount, 0f, 100f);
        stats.BarUpdate();
    }

    /// <summary>Queues healing the way medicine does: the game regains it a little each in-game minute.</summary>
    public static void HealOverTime(float amount) => Stats().health.HealthToRegenerate += amount;

    /// <summary>Full health, no bleeding, nothing queued to regenerate. Needs are left alone.</summary>
    public static void FullHeal()
    {
        var stats = Stats();
        stats.health.currentStatus = 100f;
        stats.health.HealthToRegenerate = 0f;
        stats.health.Bleeding = 0f;
        stats.BarUpdate();
    }

    /// <summary>The game's "clear needs": full health, no hunger, thirst, tiredness or cravings, clean.</summary>
    public static void ClearNeeds() => Stats().ClearAllNeeds();

    /// <summary>The game's full stat reset: needs, addictions, mushrooms, radiation, active conditions.</summary>
    public static void ResetStats() => Stats().ResetStats();

    /// <summary>Kills the player as the console's <c>kill</c> does. Blocked by god mode.</summary>
    public static void Kill(DamageTypeNames reason = DamageTypeNames.Unspecified)
    {
        var stats = Stats();
        if (stats.Godmode)
            return;
        stats.health.currentStatus = 0f;
        stats.death(reason);
    }

    /// <summary>Health, 0 to 100, or null without a player.</summary>
    public static float? Health =>
        PlayerStats.instance != null && PlayerStats.instance.health != null ? PlayerStats.instance.health.currentStatus : (float?)null;

    private static PlayerStats Stats() =>
        PlayerStats.instance != null ? PlayerStats.instance : throw new InvalidOperationException("There is no player right now (main menu or loading).");
}
