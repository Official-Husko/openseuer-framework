using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Patching;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// Turns trader and vending machine purchases into <see cref="Shops.Bought"/> and <see cref="Shops.Sold"/>.
/// <c>Trade.Buy</c> and <c>Trade.Sell</c> are named from the player's side and overridden by
/// <c>TradeSpecial</c> without calling the base, so all four are patched. Prices are read before the game
/// moves the money, through the same price method the game charges with, so they include any
/// <see cref="Prices"/> modifier.
/// </summary>
[HarmonyPatch]
[PatchArea("Shops")]
internal static class TraderDealPatches
{
    internal struct State
    {
        public bool Active;
        public Item Item;
        public int Amount;
        public float Price;
        public Currency Currency;
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        var args = new[] { typeof(ItemData), typeof(int) };
        yield return AccessTools.Method(typeof(Trade), nameof(Trade.Buy), args);
        yield return AccessTools.Method(typeof(Trade), nameof(Trade.Sell), args);
        yield return AccessTools.Method(typeof(TradeSpecial), nameof(TradeSpecial.Buy), args);
        yield return AccessTools.Method(typeof(TradeSpecial), nameof(TradeSpecial.Sell), args);
    }

    [HarmonyPrefix]
    private static void Before(Trade __instance, ItemData itemData, int amount, MethodBase __originalMethod, out State __state)
    {
        __state = default;
        try
        {
            bool buying = __originalMethod.Name == nameof(Trade.Buy);
            if (amount <= 0 || itemData == null || itemData.item == null || itemData.slotController == null)
                return;
            if (!(buying ? Shops.Bought : Shops.Sold).HasSubscribers)
                return;

            var currency = __instance.GetItemCurrency(itemData);
            __state = new State
            {
                Active = true,
                Item = itemData.item,
                Amount = amount,
                Currency = currency,
                Price = __instance.GetItemStackPrice(itemData, amount, currency),
            };
        }
        catch (Exception e)
        {
            WorldLog.Failed("Trade hook (before)", e);
        }
    }

    [HarmonyPostfix]
    private static void After(Trade __instance, bool __result, MethodBase __originalMethod, State __state)
    {
        if (!__state.Active || !__result)
            return;
        try
        {
            bool buying = __originalMethod.Name == nameof(Trade.Buy);
            var shop = ShopIndex.Of(__instance);
            if (shop == null)
                return;

            int oc = __state.Currency == Currency.OC ? (int)Math.Round(__state.Price) : 0;
            float rm = __state.Currency == Currency.RM ? Money.RoundRM(__state.Price) : 0f;
            var deal = new ShopDeal(shop, buying, __state.Item, null, __state.Amount, oc, rm);
            (buying ? Shops.Bought : Shops.Sold).Raise(deal);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Trade hook (after)", e);
        }
    }
}

/// <summary>Vending machine purchases. The game decrements <c>currentStock</c> only when the payment went through.</summary>
[HarmonyPatch(typeof(VendingMachine), nameof(VendingMachine.Buy))]
[PatchArea("Shops")]
internal static class VendingDealPatches
{
    internal struct State
    {
        public bool Active;
        public int Stock;
        public float Price;
    }

    [HarmonyPrefix]
    private static void Before(VendingMachine.VendingMachineItem item, out State __state)
    {
        __state = default;
        try
        {
            if (item == null || !Shops.Bought.HasSubscribers)
                return;
            __state = new State { Active = true, Stock = item.currentStock, Price = item.GetPrice() };
        }
        catch (Exception e)
        {
            WorldLog.Failed("Vending hook (before)", e);
        }
    }

    [HarmonyPostfix]
    private static void After(VendingMachine __instance, VendingMachine.VendingMachineItem item, State __state)
    {
        if (!__state.Active || item.currentStock >= __state.Stock)
            return;
        try
        {
            var shop = ShopIndex.Of(__instance);
            var goods = item.item != null ? item.item.Item : null;
            if (shop == null || goods == null)
                return;

            int oc = item.currency == Currency.OC ? (int)Math.Round(__state.Price) : 0;
            float rm = item.currency == Currency.RM ? Money.RoundRM(__state.Price) : 0f;
            Shops.Bought.Raise(new ShopDeal(shop, true, goods, null, 1, oc, rm));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Vending hook (after)", e);
        }
    }
}
