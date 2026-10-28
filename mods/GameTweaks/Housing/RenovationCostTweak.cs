// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace GameTweaks.Housing;

/// <summary>
/// Scales both renovation payments: the coins paid when work starts and the Real Money of the final bill.
/// The upgrade screen, the payment, and the framework's <c>Renovations</c> all read the two price getters,
/// so the change shows and is charged the same everywhere. A renovation already paid for stays paid.
/// </summary>
internal sealed class RenovationCostTweak : TweakFeature
{
    public const string Key = "RenovationCost";

    internal static float Factor = 1f;

    public override string Id => Key;
    public override string Name => "Cheaper (or dearer) renovations";
    public override string Description =>
        "Both renovation payments, the first in coins and the final bill in Real Money, are scaled by the factor on the Settings page.";

    public override void Apply(TweaksConfig config) => Factor = TweakMath.Factor(config.RenovationCost, 0f, 100f);

    protected override void OnDisable() => Factor = 1f;
}

[HarmonyPatchCategory(RenovationCostTweak.Key)]
[HarmonyPatch]
internal static class RenovationCostPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementUpgrade), nameof(TenementUpgrade.GetPriceStartOC))]
    private static void StartPrice(ref int __result) => __result = TweakMath.Price(__result, RenovationCostTweak.Factor);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementUpgrade), nameof(TenementUpgrade.GetPriceFinalRM))]
    private static void FinalPrice(ref int __result) => __result = TweakMath.Price(__result, RenovationCostTweak.Factor);
}
