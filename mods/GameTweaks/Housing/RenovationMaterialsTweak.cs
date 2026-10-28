// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace GameTweaks.Housing;

/// <summary>
/// Renovations need only money. The game checks a renovation's materials through
/// <c>TenementController.CheckForItem</c> (only the upgrade screen and <c>TenementUpgrade.CanUpgrade</c>
/// call it) and takes them through <c>TenementController.RemoveRequiredItems</c> (only called when an order
/// is paid). While this is on the first answers "you have them" and the second takes nothing.
/// </summary>
internal sealed class RenovationMaterialsTweak : TweakFeature
{
    public const string Key = "RenovationMoneyOnly";

    public override string Id => Key;
    public override string Name => "Renovations need only money";
    public override string Description =>
        "Renovations no longer need building materials from your storage or inventory, only the payment.";

    public override void Apply(TweaksConfig config) { }
}

[HarmonyPatchCategory(RenovationMaterialsTweak.Key)]
[HarmonyPatch]
internal static class RenovationMaterialsPatches
{
    // Ahead of every other prefix, including the framework's extra inventory sources, so those see nothing
    // to take; Harmony still runs the other prefixes when one skips the original.
    private const int BeforeOthers = Priority.First + 100;

    [HarmonyPrefix]
    [HarmonyPriority(BeforeOthers)]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.CheckForItem))]
    private static bool HasMaterials(ref bool __result)
    {
        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPriority(BeforeOthers)]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.RemoveRequiredItems))]
    private static bool TakeMaterials(ref int amount)
    {
        amount = 0;
        return false;
    }
}
