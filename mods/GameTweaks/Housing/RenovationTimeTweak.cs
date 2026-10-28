// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace GameTweaks.Housing;

/// <summary>
/// Renovations take less (or more) time, or none. An order's time is fixed when it is placed
/// (<c>TenementUpgradeProgress.AddUpgrade</c>, private, the one place both the building and the apartments
/// go through), so only orders placed from now on change; orders in progress keep their saved time. The
/// hours the upgrade screen shows (<c>GetRequiredTimeHours</c>) are scaled to match. Upgrades the story
/// starts itself (<c>TenementUpgradeStart.Upgrade</c>) keep their scripted time.
/// </summary>
internal sealed class RenovationTimeTweak : TweakFeature
{
    public const string Key = "RenovationTime";

    internal static float Factor = 1f;
    internal static bool Instant;

    public override string Id => Key;
    public override string Name => "Faster (or slower) renovations";
    public override string Description =>
        "New renovation orders take the time set on the Settings page, or finish at once. Work already under way keeps its time.";

    public override string AppliesWhen => "to orders placed from now on";

    public override void Apply(TweaksConfig config)
    {
        Factor = TweakMath.Factor(config.RenovationTime, 0.01f, 100f);
        Instant = config.InstantRenovations;
    }

    protected override void OnDisable()
    {
        Factor = 1f;
        Instant = false;
        RenovationTimePatches.Scripted = false;
    }
}

[HarmonyPatchCategory(RenovationTimeTweak.Key)]
[HarmonyPatch]
internal static class RenovationTimePatches
{
    /// <summary>True while the story starts an upgrade, whose time is left alone.</summary>
    internal static bool Scripted;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementUpgradeProgress), "AddUpgrade")]
    private static void Placing(ref float timeHoursLeft) =>
        timeHoursLeft = TweakMath.OrderHours(timeHoursLeft, RenovationTimeTweak.Factor, RenovationTimeTweak.Instant, Scripted);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementUpgrade), nameof(TenementUpgrade.GetRequiredTimeHours))]
    private static void Shown(ref int __result) => __result = TweakMath.Hours(__result, RenovationTimeTweak.Factor);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementUpgradeStart), nameof(TenementUpgradeStart.Upgrade))]
    private static void StoryStarts(out bool __state)
    {
        __state = Scripted;
        Scripted = true;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TenementUpgradeStart), nameof(TenementUpgradeStart.Upgrade))]
    private static void StoryStarted(bool __state) => Scripted = __state;
}
