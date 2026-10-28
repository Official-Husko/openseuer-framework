using System;
using HarmonyLib;

namespace ObenFramework.Npcs;

/// <summary>
/// The one hook the phone numbers need: <c>Phone.StartCall</c>, which the dialing screen and an answered
/// ringing phone both go through. It lets a mod line run its callback and, for a line without a conversation,
/// hang up instead of opening an empty one.
/// </summary>
[HarmonyPatch]
internal static class PhonePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone), nameof(Phone.StartCall))]
    private static bool BeforeStartCall(Phone __instance, PhoneNumber number)
    {
        try { return PhoneNumbers.HandleCall(__instance, number); }
        catch (Exception e)
        {
            NpcsLog.Failed("Phone call hook", e);
            return true;
        }
    }
}
