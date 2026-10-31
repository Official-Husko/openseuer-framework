using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// A hotel counts its rooms' stays down once a day after noon, when the clock's day is past its last check. It then
/// stores "lastCheck = today", which is the clock's own TimeAndDay object, so from then on it compares the clock
/// with itself and never sees another day until its level is loaded again (the saved lastCheck is a copy). Stays
/// ran out only after leaving and coming back, other guests' bookings stood still and the daily price never
/// changed. The fix keeps a copy instead.
/// </summary>
internal sealed class HotelDayOnce : ModFeature
{
    public const string Key = "HotelDayOnce";

    public override string Id => Key;
    public override string Name => "Hotel stays count every day";
    public override string Description => "Hotel and pod stays run out after the nights you paid for, even if you never leave the hotel's level.";
}

[HarmonyPatchCategory(HotelDayOnce.Key)]
[HarmonyPatch(typeof(Hotel), "CurrentTime")]
internal static class HotelDayOncePatch
{
    private static void Postfix(TimeAndDay today, ref TimeAndDay ___lastCheck)
    {
        if (today == null || !ReferenceEquals(___lastCheck, today))
            return;
        var copy = new TimeAndDay();
        copy.CopyValues(today);
        ___lastCheck = copy;
    }
}
