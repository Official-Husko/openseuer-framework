using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// Blowing out a candle lantern (Lantern.Interact calls CandleExtinguished) drops the lantern's candle timer but
/// leaves it running on the game clock. When it runs out it calls CandleExtinguished again: a candle lit in the
/// meantime went out at the old candle's end time, and an empty lantern played the burn-out sound and fired its
/// burnt-out relay. The fix stops a timer that still has time left when the candle goes out.
/// </summary>
internal sealed class LanternStaleTimer : ModFeature
{
    public const string Key = "LanternStaleTimer";

    public override string Id => Key;
    public override string Name => "Relit lantern candles burn their full time";
    public override string Description => "A candle lit in a lantern that was blown out earlier burns for its own time, not until the old candle's end.";
}

[HarmonyPatchCategory(LanternStaleTimer.Key)]
[HarmonyPatch(typeof(Lantern), nameof(Lantern.CandleExtinguished))]
internal static class LanternStaleTimerPatch
{
    private static void Prefix(TimeOfDayAzure.Timer ___candleTimer)
    {
        // A timer that ran out is calling this itself and is taken off the clock right after.
        if (___candleTimer != null && ___candleTimer.remainingInSeconds > 0)
            ___candleTimer.Destroy();
    }
}
