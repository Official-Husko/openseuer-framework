using System;
using HarmonyLib;
using ObenFramework.Patching;
using ObenFramework.World;

namespace ObenFramework.Economy;

/// <summary>
/// The three ways a shop gets new goods: <c>Storage.SpawnItems</c> (a trader's storage refilling; it also runs
/// for the storage's first fill), <c>VendingMachine.RefillStock</c> and <c>BaseShop.Restock</c>. Stock added
/// through <see cref="ShopStock"/> is topped up first, then <see cref="Shops.Restocked"/> is raised.
/// </summary>
[HarmonyPatch]
[PatchArea("Shops")]
internal static class RestockPatches
{
    [HarmonyPatch(typeof(Storage), nameof(Storage.SpawnItems))]
    [HarmonyPostfix]
    private static void TraderRestocked(Storage __instance)
    {
        try
        {
            var trade = __instance.GetComponent<Trade>();
            if (trade == null)
                return;
            ShopStockTraders.Restocked(trade);
            Announce(trade);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Trader restock hook", e);
        }
    }

    [HarmonyPatch(typeof(VendingMachine), nameof(VendingMachine.RefillStock))]
    [HarmonyPostfix]
    private static void VendingRestocked(VendingMachine __instance)
    {
        try { Announce(__instance); }
        catch (Exception e) { WorldLog.Failed("Vending restock hook", e); }
    }

    [HarmonyPatch(typeof(BaseShop), nameof(BaseShop.Restock))]
    [HarmonyPostfix]
    private static void ShopRestocked(BaseShop __instance)
    {
        try { Announce(__instance); }
        catch (Exception e) { WorldLog.Failed("Shop restock hook", e); }
    }

    private static void Announce(UnityEngine.Component component)
    {
        // The game restocks while it loads a save and while scenes start up; those aren't news.
        if (!WorldChanges.Active || !Shops.Restocked.HasSubscribers)
            return;
        var shop = ShopIndex.Of(component);
        if (shop != null)
            Shops.Restocked.Raise(shop);
    }
}
