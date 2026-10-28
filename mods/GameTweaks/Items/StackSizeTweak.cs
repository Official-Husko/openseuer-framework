// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace GameTweaks.Items;

/// <summary>
/// Scales how many of an item fit in one slot. Every stack check in the game reads <c>Item.Stackable</c>
/// (the base size after the difficulty's stack setting), so one postfix there covers the inventory, storage,
/// shops and liquid capacity per slot. Items that don't stack stay single.
/// </summary>
internal sealed class StackSizeTweak : TweakFeature
{
    public const string Key = "StackSize";

    internal static float Factor = 1f;

    public override string Id => Key;
    public override string Name => "Bigger (or smaller) stacks";
    public override string Description =>
        "Items that stack fit more to a slot, by the factor on the Settings page. Stacks already over a lower limit stay as they are.";

    public override void Apply(TweaksConfig config) => Factor = TweakMath.Factor(config.StackSize, 0.05f, 100f);

    protected override void OnDisable() => Factor = 1f;
}

[HarmonyPatchCategory(StackSizeTweak.Key)]
[HarmonyPatch(typeof(Item), nameof(Item.Stackable), MethodType.Getter)]
internal static class StackSizePatch
{
    private static void Postfix(ref int __result) => __result = TweakMath.Stack(__result, StackSizeTweak.Factor);
}
