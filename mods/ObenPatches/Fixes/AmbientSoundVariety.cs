using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// The coroutine that plays the random ambient sounds of an area (SoundscapeArea.PlayRandomSoundsCoroutine)
/// has two slips. It sets the highest random pitch from the lowest one (<c>pitchMax = randomPitchMin</c>),
/// so a sound set up with a pitch range always plays at its low end. And it picks the clip with
/// <c>Random.Range(0, Length - 1)</c>, which can never choose the last clip. The fix reads the pitch
/// from randomPitchMax and lets the last clip be picked.
/// </summary>
internal sealed class AmbientSoundVariety : ModFeature
{
    public const string Key = "AmbientSoundVariety";

    public override string Id => Key;
    public override string Name => "Ambient sounds vary as intended";
    public override string Description => "Random ambient sounds use their whole pitch range and all of their clips.";
}

[HarmonyPatchCategory(AmbientSoundVariety.Key)]
[HarmonyPatch]
internal static class AmbientSoundVarietyPatch
{
    private static readonly FieldInfo PitchMin = AccessTools.Field(typeof(SoundscapeSoundRandom), nameof(SoundscapeSoundRandom.randomPitchMin));
    private static readonly FieldInfo PitchMax = AccessTools.Field(typeof(SoundscapeSoundRandom), nameof(SoundscapeSoundRandom.randomPitchMax));

    // The coroutine's compiler-generated state machine, found by name since its number can change.
    private static MethodBase TargetMethod() =>
        typeof(SoundscapeArea)
            .GetNestedTypes(BindingFlags.NonPublic)
            .FirstOrDefault(t => t.Name.Contains("PlayRandomSoundsCoroutine"))
            ?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var list = IlPatterns.IncludeLastIndex(code, "SoundscapeArea.PlayRandomSoundsCoroutine");
        bool pitchFixed = false;
        for (int i = 0; i + 1 < list.Count; i++)
        {
            if (list[i].LoadsField(PitchMin) && list[i + 1].operand is FieldInfo target && target.Name.Contains("pitchMax"))
            {
                list[i].operand = PitchMax;
                pitchFixed = true;
            }
        }
        if (!pitchFixed)
            PatchLog.Log.Warn("SoundscapeArea.PlayRandomSoundsCoroutine: pitchMax assignment not found, leaving the pitch as it is");
        return list;
    }
}
