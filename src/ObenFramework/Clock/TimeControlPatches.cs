// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Logging;
using UnityEngine.AzureSky;

namespace ObenFramework.Clock;

/// <summary>
/// The clock hook behind <see cref="TimeControl"/>. TimeOfDayAzure hands the time controller its day
/// length every frame (scaled by the game's own speed factor); a shorter day runs time faster and 0 stops
/// it. With no override held this is one static check.
/// </summary>
[HarmonyPatch(typeof(AzureTimeController), nameof(AzureTimeController.SetNewDayLength))]
internal static class TimeControlPatches
{
    private static readonly AreaLog Log = new AreaLog("clock");

    private static void Prefix(ref float value)
    {
        if (!TimeControl.Overrides.Any)
            return;
        try
        {
            value = TimeControl.AdjustDayLength(value);
        }
        catch (Exception e)
        {
            Log.Failed("TimeControl day length", e);
        }
    }
}
