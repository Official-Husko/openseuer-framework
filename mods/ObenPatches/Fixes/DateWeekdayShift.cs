using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// TimeAndDay.GetWeekday takes .NET's day of the week, where Sunday is 0, and maps 0 to Monday, so every weekday
/// it reports is a day late. Autofarms use it for their replant days (the Potato Mine's Monday, Wednesday and
/// Friday replant ran on Tuesday, Thursday and Saturday) and date relays for weekly events. The clock's own
/// weekDay is right, and doors and rent day use that. The fix moves the answer back by one day.
/// </summary>
internal sealed class DateWeekdayShift : ModFeature
{
    public const string Key = "DateWeekdayShift";

    public override string Id => Key;
    public override string Name => "Farms replant on their days";
    public override string Description => "Autofarms and other schedules set to certain weekdays run on those days instead of a day later.";
}

[HarmonyPatchCategory(DateWeekdayShift.Key)]
[HarmonyPatch(typeof(TimeAndDay), nameof(TimeAndDay.GetWeekday))]
internal static class DateWeekdayShiftPatch
{
    private static void Postfix(ref Weekday __result)
    {
        if (__result == Weekday.Nullday)
            return;
        __result = __result == Weekday.Monday ? Weekday.Sunday : __result - 1;
    }
}
