// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using UnityEngine.AzureSky;

namespace GameTweaks.Clock;

/// <summary>
/// Makes in-game days last longer (or pass faster). Every frame the game hands the sky controller the
/// length of a day in real minutes (<c>TimeOfDayAzure.actualSpeed</c>: the level's day length divided by the
/// sleep, wait and console speed factor) through <c>AzureTimeController.SetNewDayLength</c>, and the clock,
/// the timers and every stat tick follow that controller. Dividing the value there scales all of it while
/// leaving <c>dayLenghtInMinutes</c> alone, which is part of the save, so nothing of the tweak is ever saved.
/// Sleeping and waiting still speed time up on top, and the speed factor they use (the framework's
/// <c>GameClock.TimeSpeed</c>) is untouched. Two small things read the unscaled day length: the power meters'
/// usage per real second, and where an NPC is placed along its path when a level loads.
/// </summary>
internal sealed class DayLengthTweak : TweakFeature
{
    public const string Key = "DayLength";

    internal static float Speed = 1f;

    public override string Id => Key;
    public override string Name => "Longer (or shorter) days";
    public override string Description =>
        "The clock runs at the speed on the Settings page: 0.5 makes every day last twice as long. Sleeping and waiting still speed it up.";

    public override void Apply(TweaksConfig config) => Speed = TweakMath.Factor(config.TimeSpeed, 0.01f, 100f);

    protected override void OnDisable() => Speed = 1f;
}

[HarmonyPatchCategory(DayLengthTweak.Key)]
[HarmonyPatch(typeof(AzureTimeController), nameof(AzureTimeController.SetNewDayLength))]
internal static class DayLengthPatch
{
    private static void Prefix(ref float value) => value = TweakMath.Faster(value, DayLengthTweak.Speed);
}
