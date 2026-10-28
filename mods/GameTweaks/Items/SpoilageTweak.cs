// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using OS.Items;

namespace GameTweaks.Items;

/// <summary>
/// Scales how fast food and other perishables lose integrity. Both ways the game spoils an item (a stack
/// checked when it is looked at, and storage ticking its contents) end in
/// <c>ItemPerishabledata.RemoveMinutesFromIntegrity</c>, so the minutes passed in are scaled there.
/// Spoiling that already happened stays; the item's integrity is part of the save.
/// </summary>
internal sealed class SpoilageTweak : TweakFeature
{
    public const string Key = "Spoilage";

    internal static float Factor = 1f;

    public override string Id => Key;
    public override string Name => "Food spoils slower (or faster)";
    public override string Description =>
        "Perishables lose freshness at the rate on the Settings page. 0.5 makes them last twice as long, 0 stops spoiling.";

    public override void Apply(TweaksConfig config) => Factor = TweakMath.Factor(config.Spoilage, 0f, 100f);

    protected override void OnDisable() => Factor = 1f;
}

[HarmonyPatchCategory(SpoilageTweak.Key)]
[HarmonyPatch(typeof(ItemPerishabledata), nameof(ItemPerishabledata.RemoveMinutesFromIntegrity))]
internal static class SpoilagePatch
{
    private static void Prefix(ref float minutes) => minutes = TweakMath.Spoil(minutes, SpoilageTweak.Factor);
}
