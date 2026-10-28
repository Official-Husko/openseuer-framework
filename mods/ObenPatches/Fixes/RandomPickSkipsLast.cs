using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// Several places pick a random entry with <c>Random.Range(0, list.Count - 1)</c>. The int overload
/// already leaves out the upper bound, so the last entry can never be picked. The worst case is the
/// A* patrol task: with two waypoints, an NPC standing at the first one loops forever looking for a
/// different waypoint, because the only value it can roll is 0 (the game's other Patrol task, which
/// uses plain <c>Count</c>, shows what was meant). The same slip leaves the last fish type out of the
/// fish spawner, the last slot out of the storage "remove random items" effect, and the last
/// object out of the "find with tag, random" behaviour task. The fix drops the "- 1" in those methods.
/// </summary>
internal sealed class RandomPickSkipsLast : ModFeature
{
    public const string Key = "RandomPickSkipsLast";

    public override string Id => Key;
    public override string Name => "Random picks can choose the last entry";
    public override string Description => "NPC patrols can no longer hang on two waypoints, and random choices in a few places stop skipping the last entry.";
}

[HarmonyPatchCategory(RandomPickSkipsLast.Key)]
[HarmonyPatch]
internal static class RandomPickSkipsLastPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // The behaviour task assembly isn't referenced, so those are looked up by name.
        const string tasks = "BehaviorDesigner.Runtime.Tasks.";
        var targets = new[]
        {
            AccessTools.Method(tasks + "Movement.AstarPathfindingProject.Patrol:OnUpdate"),
            AccessTools.Method(tasks + "Movement.AstarPathfindingProject.PatrolWithAnimations:OnUpdate"),
            AccessTools.Method(tasks + "Basic.UnityGameObject.FindWithTag:OnUpdate"),
            AccessTools.Method(typeof(Storage), nameof(Storage.RemoveRandom)),
            AccessTools.Method(typeof(Fishspawner), "DeltaSeconds"),
        };
        foreach (var method in targets)
        {
            if (method != null)
                yield return method;
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, MethodBase original) =>
        IlPatterns.IncludeLastIndex(code, original.DeclaringType.Name + "." + original.Name);
}
