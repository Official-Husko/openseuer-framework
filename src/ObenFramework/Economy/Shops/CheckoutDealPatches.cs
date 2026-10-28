using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.Patching;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// Supermarket checkouts. <c>MarketShop.Pay</c> charges the scanned total and releases the goods; there
/// is no per-item call, so the goods on the conveyor are listed before paying and reported one deal per
/// item once the balance has dropped. Item prices are the unrounded scan prices, so the deals of one checkout
/// can differ from the charged total by the rounding of that total.
/// </summary>
[HarmonyPatch(typeof(MarketShop), nameof(MarketShop.Pay))]
[PatchArea("Shops")]
internal static class MarketDealPatches
{
    internal sealed class State
    {
        public readonly List<ShopDeal> Lines = new List<ShopDeal>();
        public float Before;
    }

    [HarmonyPrefix]
    private static void Before(MarketShop __instance, List<MarketShop.ConveyorItem> ___items, out State __state)
    {
        __state = null;
        try
        {
            var shop = ShopIndex.Of(__instance);
            if (shop == null || !Shops.Bought.HasSubscribers)
                return;

            __state = new State { Before = Wallet.Get(__instance.currency) };
            foreach (var conveyor in ___items)
                AddLines(__instance, shop, conveyor, __state.Lines);
        }
        catch (Exception e)
        {
            __state = null;
            WorldLog.Failed("Checkout hook (before)", e);
        }
    }

    [HarmonyPostfix]
    private static void After(MarketShop __instance, State __state)
    {
        if (__state == null || Wallet.Get(__instance.currency) >= __state.Before)
            return;
        try
        {
            foreach (var line in __state.Lines)
                Shops.Bought.Raise(line);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Checkout hook (after)", e);
        }
    }

    private static void AddLines(MarketShop market, ShopInfo shop, MarketShop.ConveyorItem conveyor, List<ShopDeal> lines)
    {
        if (conveyor.gameObject == null)
            return;

        var single = conveyor.gameObject.GetComponentInChildren<CollectibleItem>();
        if (single != null)
        {
            Add(market, shop, single.Item != null ? single.Item.Item : null, single.meta, single.amount, lines);
            return;
        }

        var package = conveyor.gameObject.GetComponent<CollectiblePackage>();
        if (package == null)
            return;
        foreach (var stack in package.items)
        {
            if (stack == null || stack.itemAmount <= 0 || stack.itemId < 0)
                continue;
            var item = stack.itemReference != null ? stack.itemReference.Item : null;
            Add(market, shop, item, stack.Meta, stack.itemAmount, lines);
        }
    }

    private static void Add(MarketShop market, ShopInfo shop, Item item, object[] meta, int amount, List<ShopDeal> lines)
    {
        if (item == null || amount <= 0)
            return;
        float price = market.GetItemSalePrice(item, meta, false) * amount;
        int oc = market.currency == Currency.OC ? (int)Math.Round(price) : 0;
        float rm = market.currency == Currency.RM ? Money.RoundRM(price) : 0f;
        lines.Add(new ShopDeal(shop, true, item, null, amount, oc, rm));
    }
}

/// <summary>
/// Furniture and train shops. <c>ShopBaseUI.Buy</c> and <c>Sell</c> are private and check money only inside, so
/// a deal counts when the balance moved. A free piece of furniture changes no balance and is not reported.
/// </summary>
[HarmonyPatch]
[PatchArea("Shops")]
internal static class FurnitureDealPatches
{
    internal struct State
    {
        public bool Active;
        public int Oc;
        public float Rm;
        public Furniture Furniture;
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(ShopBaseUI), "Buy");
        yield return AccessTools.Method(typeof(ShopBaseUI), "Sell");
    }

    [HarmonyPrefix]
    private static void Before(ShopBaseUI __instance, MethodBase __originalMethod, out State __state)
    {
        __state = default;
        try
        {
            bool buying = __originalMethod.Name == "Buy";
            if (!(buying ? Shops.Bought : Shops.Sold).HasSubscribers)
                return;
            var selection = __instance.currentSelection;
            __state = new State
            {
                Active = true,
                Oc = Wallet.Coins,
                Rm = Wallet.RealMoney,
                Furniture = selection != null && selection.buildObject != null ? selection.buildObject.furniture : null,
            };
        }
        catch (Exception e)
        {
            WorldLog.Failed("Furniture shop hook (before)", e);
        }
    }

    [HarmonyPostfix]
    private static void After(BaseShop ___currentShop, MethodBase __originalMethod, State __state)
    {
        if (!__state.Active)
            return;
        try
        {
            bool buying = __originalMethod.Name == "Buy";
            int oc = buying ? __state.Oc - Wallet.Coins : Wallet.Coins - __state.Oc;
            float rm = Money.RoundRM(buying ? __state.Rm - Wallet.RealMoney : Wallet.RealMoney - __state.Rm);
            var shop = ShopIndex.Of(___currentShop);
            if (shop == null || (oc <= 0 && rm <= 0f))
                return;

            var deal = new ShopDeal(shop, buying, null, __state.Furniture, 1, Math.Max(0, oc), Math.Max(0f, rm));
            (buying ? Shops.Bought : Shops.Sold).Raise(deal);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Furniture shop hook (after)", e);
        }
    }
}
