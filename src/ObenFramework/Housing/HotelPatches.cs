// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>Raises <see cref="Hotels.RoomChanged"/> when the player rents a room.</summary>
[HarmonyPatch(typeof(HotelRoom), nameof(HotelRoom.RentRoomPlayer))]
internal static class HotelRentPatch
{
    [HarmonyPostfix]
    private static void After(HotelRoom __instance, int nights, bool __result)
    {
        if (__result)
            HotelStayPatch.Raise(__instance, true, nights);
    }
}

/// <summary>Raises <see cref="Hotels.RoomChanged"/> when the daily count ends the player's stay (both overloads of <c>CountDays</c>).</summary>
[HarmonyPatch]
internal static class HotelStayPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(HotelRoom), "CountDays", Type.EmptyTypes);
        yield return AccessTools.Method(typeof(HotelRoom), "CountDays", new[] { typeof(bool).MakeByRefType() });
    }

    [HarmonyPrefix]
    private static void Before(HotelRoom __instance, out bool __state) => __state = __instance.rentedForPlayer;

    [HarmonyPostfix]
    private static void After(HotelRoom __instance, bool __state)
    {
        if (__state && !__instance.rentedForPlayer)
            Raise(__instance, false, 0);
    }

    internal static void Raise(HotelRoom room, bool rented, int nights)
    {
        if (!Hotels.RoomChanged.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            Hotels.RoomChanged.Raise(new HotelRoomChange(room, rented, nights, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Hotel hook", e);
        }
    }
}
