using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// The urge to gamble darkens the mood through the same curve as the other addictions, but
/// PlayerStatsMentalHealth.MentalHealthStatsUpdate adds it under "if (SmokingNeed > 0)", copied from the smoking
/// line above it, so a non-smoker's gambling urge never counted (measured: an urge of 80 raised the mood's floor by
/// 0.51 like a smoking urge of 80 does, but only for smokers). The fix makes that test read the gambling urge.
/// </summary>
internal sealed class GamblingNeedMood : ModFeature
{
    public const string Key = "GamblingNeedMood";

    public override string Id => Key;
    public override string Name => "Gambling urge affects the mood";
    public override string Description => "The urge to gamble weighs on the mood like the other cravings, also when you don't smoke.";
}

[HarmonyPatchCategory(GamblingNeedMood.Key)]
[HarmonyPatch(typeof(PlayerStatsMentalHealth), "MentalHealthStatsUpdate")]
internal static class GamblingNeedMoodPatch
{
    private static readonly FieldInfo Smoking = AccessTools.Field(typeof(PlayerStatsAddictions), nameof(PlayerStatsAddictions.SmokingNeed));
    private static readonly FieldInfo Gambling = AccessTools.Field(typeof(PlayerStatsAddictions), nameof(PlayerStatsAddictions.GamblingNeed));

    // A smoking read whose next need read is the gambling one is the copied test.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = code.ToList();
        int changed = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].LoadsField(Smoking))
                continue;
            int next = list.FindIndex(i + 1, c => c.LoadsField(Smoking) || c.LoadsField(Gambling));
            if (next < 0 || !list[next].LoadsField(Gambling))
                continue;
            list[i].operand = Gambling;
            changed++;
        }
        if (changed == 0)
            PatchLog.Log.Warn("PlayerStatsMentalHealth.MentalHealthStatsUpdate: no smoking test before the gambling urge found, leaving it as it is");
        return list;
    }
}
