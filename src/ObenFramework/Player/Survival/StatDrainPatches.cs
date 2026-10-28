// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace ObenFramework.Player.Survival;

/// <summary>
/// Applies <see cref="StatDrain"/> to the game's rate getters. Every stat tick asks these for the current
/// per-hour rate, so the change takes effect on the next minute of game time. With no adjustments set
/// each postfix is a multiply by 1 and an add of 0.
/// </summary>
[HarmonyPatch]
internal static class StatDrainPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsHungerAndThirst), nameof(PlayerStatsHungerAndThirst.GetHungerRate))]
    private static void HungerRate(ref float __result) => __result = StatDrain.Apply(PlayerDrain.Hunger, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsHungerAndThirst), nameof(PlayerStatsHungerAndThirst.GetThirstRate))]
    private static void ThirstRate(ref float __result) => __result = StatDrain.Apply(PlayerDrain.Thirst, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsHygiene), nameof(PlayerStatsHygiene.GetHygieneRate))]
    private static void HygieneRate(ref float __result) => __result = StatDrain.Apply(PlayerDrain.Hygiene, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsAddictions), nameof(PlayerStatsAddictions.GetAlcoholNeedBaserate))]
    private static void AlcoholNeedRate(ref float __result) => __result = StatDrain.Apply(PlayerDrain.AlcoholNeed, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsAddictions), nameof(PlayerStatsAddictions.GetMushroomNeedBaserate))]
    private static void MushroomNeedRate(ref float __result) => __result = StatDrain.Apply(PlayerDrain.MushroomNeed, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsAddictions), nameof(PlayerStatsAddictions.GetSmokingNeedBaserate))]
    private static void SmokingNeedRate(ref float __result) => __result = StatDrain.Apply(PlayerDrain.SmokingNeed, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsAddictions), nameof(PlayerStatsAddictions.GetGamblingNeedBaserate))]
    private static void GamblingNeedRate(ref float __result) => __result = StatDrain.Apply(PlayerDrain.GamblingNeed, __result);
}
