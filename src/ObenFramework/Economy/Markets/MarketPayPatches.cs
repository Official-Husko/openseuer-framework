// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Economy;

/// <summary>
/// <c>MarketShop.Pay</c>: charges the scanned total (at least 1) when the checkout waits for payment. The
/// prefix notes the total and the balance, the postfix reports a payment when the balance went down.
/// </summary>
[HarmonyPatch(typeof(MarketShop), nameof(MarketShop.Pay))]
internal static class MarketPayPatches
{
    internal struct State
    {
        public int Total;
        public float Before;
    }

    private static void Prefix(MarketShop __instance, out State __state)
    {
        __state = default;
        if (!MarketShops.Paid.HasSubscribers || __instance.state != MarketShop.State.Pay)
            return;
        __state.Total = Math.Max(1, __instance.GetCurrentPrice());
        __state.Before = Wallet.Get(__instance.currency);
    }

    private static void Postfix(MarketShop __instance, State __state)
    {
        if (__state.Total <= 0 || Wallet.Get(__instance.currency) >= __state.Before)
            return;
        try
        {
            var shop = ShopIndex.Of(__instance);
            if (shop != null)
                MarketShops.Paid.Raise(new CheckoutPayment(shop, __state.Total, __instance.currency));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Checkout payment hook", e);
        }
    }
}
