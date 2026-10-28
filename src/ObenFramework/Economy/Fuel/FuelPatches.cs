// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// <c>GasStation.UpdatePrices</c> (every price change ends there) and <c>GasStation.PayFuelOc</c> (paying at
/// the counter: the owed coins are charged, a receipt given and the pumps reset).
/// </summary>
[HarmonyPatch]
internal static class FuelPatches
{
    internal struct Pay
    {
        public int Cost;
        public float Litres;
    }

    [HarmonyPatch(typeof(GasStation), nameof(GasStation.UpdatePrices)), HarmonyPostfix]
    private static void PriceUpdated(GasStation __instance)
    {
        if (!FuelStations.PriceChanged.HasSubscribers || !WorldChanges.Active)
            return;
        try { FuelStations.PriceChanged.Raise(new FuelStationInfo(__instance)); }
        catch (Exception e) { WorldLog.Failed("Fuel price hook", e); }
    }

    [HarmonyPatch(typeof(GasStation), nameof(GasStation.PayFuelOc)), HarmonyPrefix]
    private static void BeforePay(GasStation __instance, out Pay __state)
    {
        __state = default;
        if (!FuelStations.FuelBought.HasSubscribers)
            return;
        __state.Cost = __instance.priceToPayOc;
        __state.Litres = FuelFields.Litres(__instance);
    }

    [HarmonyPatch(typeof(GasStation), nameof(GasStation.PayFuelOc)), HarmonyPostfix]
    private static void AfterPay(GasStation __instance, Pay __state)
    {
        // Paying resets what is owed; a refused payment (not enough coins) leaves it.
        if (__state.Cost <= 0 || __instance.priceToPayOc != 0)
            return;
        try
        {
            FuelStations.FuelBought.Raise(new FuelPurchase(new FuelStationInfo(__instance), __state.Litres, __state.Cost, Currency.OC));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Fuel payment hook", e);
        }
    }
}
