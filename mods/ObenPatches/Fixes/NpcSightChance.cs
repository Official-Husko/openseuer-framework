using System.Collections.Generic;
using HarmonyLib;
using NPC;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// NPC_FOV.CheckIfTargetIsDetected is meant to roll against a detection chance that falls from
/// "focus" (looking straight at the target) to "edge" (at the border of the field of view), but the
/// roll is <c>(float)Random.Range(0, 1)</c>, which is always 0 for the int overload. So any target
/// inside the field of view with a clear line is seen every time, and the two chances only matter
/// if they're zero. The fix rolls with Random.value. Off by default because it changes the
/// stealth balance: NPCs would notice you less near the edge of their view (0.15 on the NPC
/// checked in the game, with a check every half second).
/// </summary>
internal sealed class NpcSightChance : ModFeature
{
    public const string Key = "NpcSightChance";

    public override string Id => Key;
    public override string Name => "NPCs sometimes miss you at the edge of their view";
    public override string Description => "NPC sight uses its detection chance instead of always spotting you. Makes sneaking easier.";
    public override bool EnabledByDefault => false;
}

[HarmonyPatchCategory(NpcSightChance.Key)]
[HarmonyPatch(typeof(NPC_FOV), nameof(NPC_FOV.CheckIfTargetIsDetected))]
internal static class NpcSightChancePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) =>
        IlPatterns.ChanceFromRandomValue(code, "NPC_FOV.CheckIfTargetIsDetected");
}
