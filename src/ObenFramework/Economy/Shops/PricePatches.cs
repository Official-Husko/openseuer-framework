using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// The game's price methods, one per way of buying: <c>Trade.GetItemStackPrice</c> (traders; every trade
/// price, charge and affordability check goes through it), <c>MarketShop.GetItemSalePrice</c> (checkouts),
/// <c>VendingMachineItem.GetPrice</c> and <c>BaseShop.Get*Price</c> (furniture and train shops). Each
/// postfix hands the game's price to <see cref="ShopPricing"/> and returns at once while no modifier exists.
/// </summary>
[HarmonyPatch]
internal static class PricePatches
{
    internal static void Fail(string ownerId, Exception e) => WorldLog.Failed("Price modifier of " + ownerId, e);

    [HarmonyPatch(typeof(Trade), nameof(Trade.GetItemStackPrice))]
    [HarmonyPostfix]
    private static void TraderPrice(Trade __instance, ItemData itemData, int amount, Currency currency, ref float __result)
    {
        if (!ShopPricing.Active || amount <= 0)
            return;
        try
        {
            var item = itemData != null ? itemData.item : null;
            var slot = itemData != null && itemData.slotController != null ? itemData.slotController.slot : null;
            if (item == null || slot == null)
                return;

            // The slot is the player's when the player sells.
            float before = __result / amount;
            float unit = ShopPricing.Apply(ShopIndex.Of(__instance), item, null, !slot.playerSlot, currency,
                before, ShopPricing.TraderMinimum(currency), false);
            if (unit != before)
                __result = unit * amount;
        }
        catch (Exception e)
        {
            WorldLog.Failed("Trader price hook", e);
        }
    }

    [HarmonyPatch(typeof(MarketShop), nameof(MarketShop.GetItemSalePrice))]
    [HarmonyPostfix]
    private static void MarketPrice(MarketShop __instance, Item item, bool round, ref float __result)
    {
        if (!Prices.Active)
            return;
        try
        {
            // Totals are summed unrounded and rounded once, so an unrounded call must stay unrounded.
            __result = ApplyMarket(__instance, item, round, __result);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Market price hook", e);
        }
    }

    private static float ApplyMarket(MarketShop market, Item item, bool round, float price)
    {
        var shop = ShopIndex.Of(market);
        if (shop == null)
            return price;
        float modified = Prices.Run(shop, item, null, true, market.currency, price);
        if (modified == price)
            return price;
        modified = Math.Max(0f, modified);
        return round ? (float)Math.Round(modified) : modified;
    }

    [HarmonyPatch(typeof(VendingMachine.VendingMachineItem), nameof(VendingMachine.VendingMachineItem.GetPrice))]
    [HarmonyPostfix]
    private static void VendingPrice(VendingMachine.VendingMachineItem __instance, ref float __result)
    {
        if (!Prices.Active || __result <= 0f)
            return;
        try
        {
            var item = __instance.item != null ? __instance.item.Item : null;
            __result = ShopPricing.Apply(ShopIndex.OfVending(__instance), item, null, true, __instance.currency, __result, 1f, true);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Vending price hook", e);
        }
    }
}

/// <summary>The four furniture shop price methods, patched with one postfix that reads which one it is from the method name.</summary>
[HarmonyPatch]
internal static class FurniturePricePatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        foreach (var name in new[] { "GetOcBuyPrice", "GetRmBuyPrice", "GetOcSellPrice", "GetRmSellPrice" })
            yield return AccessTools.Method(typeof(BaseShop), name);
    }

    [HarmonyPostfix]
    private static void Price(BaseShop __instance, BuildingSystem.FurnitureInfo furnitureInfo, MethodBase __originalMethod, ref int __result)
    {
        if (!Prices.Active || __result <= 0)
            return;
        try
        {
            string name = __originalMethod.Name;
            var currency = name.Contains("Rm") ? Currency.RM : Currency.OC;
            bool playerBuys = name.Contains("Buy");
            __result = (int)ShopPricing.Apply(ShopIndex.Of(__instance), null, furnitureInfo.furniture, playerBuys, currency, __result, 1f, true);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Furniture price hook", e);
        }
    }
}
