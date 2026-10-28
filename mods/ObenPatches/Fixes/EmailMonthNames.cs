using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// TimeAndDay.GetMonthNameShort has August, September and October shifted: month 8 comes out as
/// "Sep", 9 as "Oct" and 10 as "Sep" again. It's used for the date on each email in the computer's
/// mail program. The fix returns the right names for those three months.
/// </summary>
internal sealed class EmailMonthNames : ModFeature
{
    public const string Key = "EmailMonthNames";

    public override string Id => Key;
    public override string Name => "Right month names on emails";
    public override string Description => "Emails sent in August, September and October show the right month instead of a shifted one.";
}

[HarmonyPatchCategory(EmailMonthNames.Key)]
[HarmonyPatch(typeof(TimeAndDay), nameof(TimeAndDay.GetMonthNameShort))]
internal static class EmailMonthNamesPatch
{
    private static void Postfix(TimeAndDay __instance, ref string __result)
    {
        switch (__instance.month)
        {
            case 8: __result = "Aug"; break;
            case 9: __result = "Sep"; break;
            case 10: __result = "Oct"; break;
        }
    }
}
