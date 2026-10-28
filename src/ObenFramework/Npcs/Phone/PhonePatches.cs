// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.Npcs;

/// <summary>
/// The phone hooks. <c>Phone.StartCall</c>, which the dialing screen and an answered ringing phone both go
/// through, lets a mod line run its callback and, for a line without a conversation, hang up instead of
/// opening an empty one; it also notes the number for the call events. The call and ring hooks raise
/// <see cref="Phones"/>' events.
/// </summary>
[HarmonyPatch]
internal static class PhonePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone), nameof(Phone.StartCall))]
    private static bool BeforeStartCall(Phone __instance, PhoneNumber number)
    {
        Phones.Active = number;
        try { return PhoneNumbers.HandleCall(__instance, number); }
        catch (Exception e)
        {
            NpcsLog.Failed("Phone call hook", e);
            return true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone), nameof(Phone.OnCallStarted))]
    private static void AfterCallStarted(Phone __instance)
    {
        try { Phones.RaiseStarted(__instance); }
        catch (Exception e) { NpcsLog.Failed("Phone call started hook", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone), nameof(Phone.OnCallEnded))]
    private static void AfterCallEnded(Phone __instance)
    {
        try { Phones.RaiseEnded(__instance); }
        catch (Exception e) { NpcsLog.Failed("Phone call ended hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Phone), nameof(Phone.ReceiveCall))]
    private static void BeforeReceive(Phone __instance, out bool __state) =>
        __state = Phones.Ringing.HasSubscribers && !Phones.IsRinging(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Phone), nameof(Phone.ReceiveCall))]
    private static void AfterReceive(Phone __instance, PhoneNumber number, bool __state)
    {
        if (!__state)
            return;
        try { Phones.RaiseRinging(__instance, number); }
        catch (Exception e) { NpcsLog.Failed("Phone ringing hook", e); }
    }
}
