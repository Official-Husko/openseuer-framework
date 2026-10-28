using HarmonyLib;
using ObenFramework.Features;

namespace PerformanceFixes.Stats;

/// <summary>
/// The player stats run on every tick of the game clock, nearly every frame. Each tick starts by
/// copying the keys of the pending item effects (laxatives, coffee, radiation and so on) into a
/// new list, even when nothing is pending, which is almost always. When the list of pending
/// effects is empty the loop has nothing to do, so this skips the copy.
/// </summary>
internal sealed class IdleStatEffects : ModFeature
{
    public const string Key = "IdleStatEffects";

    public override string Id => Key;
    public override string Name => "No wasted work when no item effects are pending";
    public override string Description => "Skips building a list of pending item effects on every tick when there are none.";
}

[HarmonyPatchCategory(IdleStatEffects.Key)]
[HarmonyPatch(typeof(PlayerStats), "ProcessItemSpecialEffects")]
internal static class IdleStatEffectsPatch
{
    private static bool Prefix(PlayerStats __instance) =>
        __instance.unprocessedSpecialEffects == null || __instance.unprocessedSpecialEffects.Count > 0;
}
