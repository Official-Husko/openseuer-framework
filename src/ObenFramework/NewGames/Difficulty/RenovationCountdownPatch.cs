// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.NewGames;

/// <summary>
/// Scales the countdown of tenement upgrades in progress by the renovation speed factor. The game stores
/// an upgrade's time unscaled and divides it by the speed when it is read, so speeding up the display
/// (<c>GetRenovationSpeed</c>) without speeding up the countdown would show a time the upgrade doesn't keep.
/// </summary>
[HarmonyPatch]
internal static class RenovationCountdownPatch
{
    private static float _carry;
    private static float _carryFactor = 1f;

    internal static void Reset()
    {
        _carry = 0f;
        _carryFactor = 1f;
    }

    // Upgrades count down by whole seconds; keep the fraction so a factor of 1.5 isn't rounded away.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), "UpdateProgesses")]
    private static void Countdown(ref int sec)
    {
        if (GameDifficulty.Factors.IsEmpty)
            return;
        try
        {
            float factor = GameDifficulty.FactorOf(DifficultyMultiplier.RenovationSpeed);
            if (factor != _carryFactor)
            {
                _carryFactor = factor;
                _carry = 0f;
            }
            if (factor == 1f)
                return;
            _carry += sec * factor;
            sec = (int)_carry;
            _carry -= sec;
        }
        catch (Exception e)
        {
            GameDifficulty.Failed("the renovation countdown", e);
        }
    }
}
