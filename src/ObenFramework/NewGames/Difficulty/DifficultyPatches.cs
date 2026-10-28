// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace ObenFramework.NewGames;

/// <summary>
/// Applies <see cref="GameDifficulty"/> factors on top of the game's own difficulty getters. Each postfix
/// returns at once while no mod set a factor. Renovation speed also needs the countdown of upgrades in
/// progress scaled, see <see cref="RenovationCountdownPatch"/>.
/// </summary>
[HarmonyPatch(typeof(DifficultyInfo))]
internal static class DifficultyPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetGeneralNeedsSpeed))]
    private static void Needs(ref float __result) => Scale(DifficultyMultiplier.NeedsSpeed, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetAddictionNeedsSpeed))]
    private static void AddictionNeeds(ref float __result) => Scale(DifficultyMultiplier.AddictionNeedsSpeed, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetAddictionRecoverySpeed))]
    private static void AddictionRecovery(ref float __result) => Scale(DifficultyMultiplier.AddictionRecoverySpeed, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetRenovationCost))]
    private static void RenovationCost(ref float __result) => Scale(DifficultyMultiplier.RenovationCost, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetRenovationResourceCost))]
    private static void RenovationResources(ref int __result) => Scale(DifficultyMultiplier.RenovationResourceCosts, ref __result, 1);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetRenovationSpeed))]
    private static void RenovationSpeed(ref float __result) => Scale(DifficultyMultiplier.RenovationSpeed, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetRentIncome))]
    private static void RentIncome(ref float __result) => Scale(DifficultyMultiplier.RentIncome, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetItemPrices))]
    private static void Prices(ref int __result) => Scale(DifficultyMultiplier.Prices, ref __result, 1);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetRespawningTime))]
    private static void Respawning(ref int __result) => Scale(DifficultyMultiplier.RespawningTime, ref __result, 0);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetSmvProgressionRateSpeed))]
    private static void Smv(ref float __result) => Scale(DifficultyMultiplier.SmvProgressionRate, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DifficultyInfo.GetTraderWealth))]
    private static void TraderWealth(ref float __result) => Scale(DifficultyMultiplier.TraderWealth, ref __result);

    private static void Scale(DifficultyMultiplier multiplier, ref float result)
    {
        if (!GameDifficulty.Factors.IsEmpty)
            result = GameDifficulty.Scaled(multiplier, result);
    }

    private static void Scale(DifficultyMultiplier multiplier, ref int result, int minimum)
    {
        if (!GameDifficulty.Factors.IsEmpty)
            result = GameDifficulty.Scaled(multiplier, result, minimum);
    }
}
