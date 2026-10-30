// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>One piece on offer at a furniture shop, with what one costs the player.</summary>
public sealed class FurnitureStockLine
{
    internal FurnitureStockLine(BaseShop shop, BuildingSystem.FurnitureInfo info)
    {
        Furniture = info.furniture;
        Amount = info.amount;
        Coins = shop.GetOcBuyPrice(info);
        RealMoney = shop.GetRmBuyPrice(info);
        NeedsPicture = info.furniture.canPlaceCustomImage && (info.meta == null || !info.meta.HasCustomImages);
    }

    public Furniture Furniture { get; }

    /// <summary>The game's id of the piece, what <see cref="FurnitureShops.Buy"/> takes besides the title.</summary>
    public string Id => Furniture.Id;

    public string Title => Furniture.title;

    public int Amount { get; }

    /// <summary>Coins one costs, <see cref="Prices"/> modifiers included.</summary>
    public int Coins { get; }

    /// <summary>Real Money one costs; a piece can cost both currencies at once.</summary>
    public int RealMoney { get; }

    /// <summary>A frame or poster the game asks a picture for before selling it; only its window can buy those.</summary>
    public bool NeedsPicture { get; }
}

/// <summary>
/// Buying furniture from and selling it to a furniture shop without its window, step for step as the window
/// does: the money (both currencies), the shop's till, its stock and the player's unplaced pieces.
/// </summary>
internal static class FurnitureDeals
{
    private static bool _running;

    public static TradeResult Buy(ShopInfo info, BaseShop shop, string furniture, int count)
    {
        var refusal = Check(shop, furniture, count);
        if (refusal != TradeResult.Ok)
            return refusal;
        var piece = Match(shop.GetAvailableItems(), furniture, false);
        if (piece == null || piece.amount < count)
            return TradeResult.OutOfStock;
        if (piece.furniture.canPlaceCustomImage && (piece.meta == null || !piece.meta.HasCustomImages))
            return TradeResult.Unsupported;
        long oc = (long)shop.GetOcBuyPrice(piece) * count;
        long rm = (long)shop.GetRmBuyPrice(piece) * count;
        if (oc > int.MaxValue || rm > int.MaxValue || !Wallet.CanAfford(oc, rm))
            return TradeResult.CannotAfford;

        var bought = piece.furniture;
        var meta = piece.meta;
        return Step("FurnitureShops.Buy", () =>
        {
            if (!Wallet.TryRemove(oc, rm))
                return TradeResult.CannotAfford;
            if (!DifficultyController.InfiniteTraderWealth)
            {
                shop.currentOcMoney += (int)oc;
                shop.currentRmMoney += (int)rm;
            }
            shop.RemoveItem(piece, count);
            BuildingSystem.instance.AddFurniture(bought, count, meta);
            Shops.Bought.Raise(new ShopDeal(info, true, null, bought, count, (int)oc, rm));
            return TradeResult.Ok;
        });
    }

    public static TradeResult Sell(ShopInfo info, BaseShop shop, string furniture, int count)
    {
        var refusal = Check(shop, furniture, count);
        if (refusal != TradeResult.Ok)
            return refusal;
        var piece = Match(BuildingSystem.instance.availableFurnitures, furniture, true);
        if (piece == null || piece.amount < count)
            return TradeResult.NotEnoughItems;
        if (piece.furniture.IsFree() || !shop.CanSell(piece))
            return TradeResult.NotAccepted;
        long oc = (long)shop.GetOcSellPrice(piece) * count;
        long rm = (long)shop.GetRmSellPrice(piece) * count;
        if (!DifficultyController.InfiniteTraderWealth && (shop.currentOcMoney < oc || shop.currentRmMoney < rm))
            return TradeResult.ShopCannotAfford;
        if ((long)Wallet.Coins + oc > int.MaxValue || Wallet.ToTenths(Wallet.RealMoney) + rm * 10L > int.MaxValue)
            return TradeResult.Failed;

        var sold = piece.furniture;
        var meta = piece.meta;
        return Step("FurnitureShops.Sell", () =>
        {
            BuildingSystem.instance.RemoveFurniture(piece, count);
            shop.AddItem(sold, meta, null, count);
            if (!DifficultyController.InfiniteTraderWealth)
            {
                shop.currentOcMoney -= (int)oc;
                shop.currentRmMoney -= (int)rm;
            }
            Wallet.Add(Currency.OC, oc);
            Wallet.Add(Currency.RM, rm);
            Shops.Sold.Raise(new ShopDeal(info, false, null, sold, count, (int)oc, rm));
            return TradeResult.Ok;
        });
    }

    /// <summary>The pieces a shop has on offer now.</summary>
    public static List<FurnitureStockLine> Stock(BaseShop shop)
    {
        var lines = new List<FurnitureStockLine>();
        foreach (var piece in shop.GetAvailableItems())
        {
            if (piece != null && piece.furniture != null && piece.amount > 0)
                lines.Add(new FurnitureStockLine(shop, piece));
        }
        return lines;
    }

    private static TradeResult Check(BaseShop shop, string furniture, int count)
    {
        if (count < 1 || string.IsNullOrEmpty(furniture))
            return TradeResult.BadInput;
        if (_running || SaveGate.IsBusy)
            return TradeResult.Busy;
        if (BuildingSystem.instance == null || TaskItemsManager.instance == null || !Wallet.CanChange)
            return TradeResult.NotReady;
        if (shop == null)
            return TradeResult.UnknownShop;
        // Train shops sell rolling stock that has to be put on a track.
        return shop is TrainShop ? TradeResult.Unsupported : TradeResult.Ok;
    }

    private static TradeResult Step(string what, Func<TradeResult> deal)
    {
        _running = true;
        try
        {
            using (WorldChanges.EnterMod())
                return deal();
        }
        catch (Exception e)
        {
            WorldLog.Failed(what, e);
            return TradeResult.Failed;
        }
        finally
        {
            _running = false;
        }
    }

    // A piece by the game's id first, then by its title. The player's side skips picked-up pieces that still hold things.
    private static BuildingSystem.FurnitureInfo Match(List<BuildingSystem.FurnitureInfo> list, string key, bool unplacedOnly)
    {
        if (list == null)
            return null;
        BuildingSystem.FurnitureInfo byTitle = null;
        foreach (var piece in list)
        {
            if (piece == null || piece.furniture == null || piece.amount < 1 || (unplacedOnly && piece.gameObject != null))
                continue;
            if (piece.furniture.Id == key)
                return piece;
            if (byTitle == null && string.Equals(piece.furniture.title, key, StringComparison.OrdinalIgnoreCase))
                byTitle = piece;
        }
        return byTitle;
    }
}
