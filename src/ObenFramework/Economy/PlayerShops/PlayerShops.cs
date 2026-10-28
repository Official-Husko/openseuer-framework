// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenLoader;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// Shops the player runs. The game has the beginnings of these (<c>PlayerShopManager</c>) but places none
/// and never sells anything, so the framework keeps its own: a shop is a set of shelves (every container in a
/// building area, plus containers added one by one), a markup or fixed prices, an open sign and a till. Mods
/// decide who buys (customers, workers, a vending service) and call <see cref="Sell"/>; the money waits in the
/// till until <see cref="CollectEarnings"/>. Shops are stored with the save, so they outlive the mod that
/// made them. Call on the main thread.
/// </summary>
public static partial class PlayerShops
{
    private static ModSaveData<PlayerShopSave> _save;

    /// <summary>Something was sold at a player-run shop.</summary>
    public static readonly WorldEvent<PlayerShopSale> Sold = new WorldEvent<PlayerShopSale>(nameof(Sold));

    /// <summary>Every player-run shop of the save.</summary>
    public static List<PlayerShopInfo> All()
    {
        var list = new List<PlayerShopInfo>();
        if (_save == null)
            return list;
        foreach (var record in _save.Value.Shops)
        {
            if (record != null)
                list.Add(Describe(record));
        }
        return list;
    }

    /// <summary>One shop, or null.</summary>
    public static PlayerShopInfo Get(string id)
    {
        var record = Record(id);
        return record != null ? Describe(record) : null;
    }

    /// <summary>
    /// Starts a shop, closed, with an empty till and the default markup. False when a shop with this id
    /// exists or no save is loaded. Add shelves with <see cref="SetArea"/> or <see cref="AddContainer"/>.
    /// </summary>
    /// <param name="id">A key of your choosing, unique in the save (letters, digits, dots, dashes).</param>
    public static bool Create(string id, string name = null, Currency currency = Currency.OC)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A shop id is required.", nameof(id));
        if (!Writable() || Record(id) != null)
            return false;
        _save.Value.Shops.Add(new PlayerShopRecord
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? id : name,
            RealMoney = currency == Currency.RM,
        });
        return true;
    }

    /// <summary>Closes a shop for good. Refused while money is in the till, unless <paramref name="force"/> (the money is lost).</summary>
    public static bool Delete(string id, bool force = false)
    {
        var record = Writable() ? Record(id) : null;
        if (record == null || (!force && (record.TillCoins > 0 || record.TillRealMoney > 0f)))
            return false;
        return _save.Value.Shops.Remove(record);
    }

    /// <summary>
    /// Uses every container in a building area (the game's <c>FurnitureManager</c>, by GUID; the player's
    /// apartment, a workshop, a shop space) as shelves. Null removes the area.
    /// </summary>
    public static bool SetArea(string id, string areaGuid) =>
        Edit(id, r => r.Area = string.IsNullOrEmpty(areaGuid) ? null : areaGuid);

    /// <summary>Adds one container (by its <c>Containers.IdOf</c> id) as a shelf.</summary>
    public static bool AddContainer(string id, string containerId)
    {
        if (string.IsNullOrEmpty(containerId))
            return false;
        return Edit(id, r =>
        {
            if (!r.Containers.Contains(containerId))
                r.Containers.Add(containerId);
        });
    }

    public static bool RemoveContainer(string id, string containerId) =>
        Edit(id, r => r.Containers.Remove(containerId));

    public static bool SetOpen(string id, bool open) => Edit(id, r => r.Open = open);

    public static bool SetName(string id, string name) =>
        !string.IsNullOrWhiteSpace(name) && Edit(id, r => r.Name = name);

    /// <summary>The factor on an item's sale value for items without a fixed price (1.2 by default, at most 10).</summary>
    public static bool SetMarkup(string id, float markup) =>
        PlayerShopMath.ValidMarkup(markup) && Edit(id, r => r.Markup = markup);

    /// <summary>Changes what the shop sells for. Fixed prices are kept as numbers, so set them again for the new currency.</summary>
    public static bool SetCurrency(string id, Currency currency) =>
        Edit(id, r => r.RealMoney = currency == Currency.RM);

    /// <summary>A fixed price for one item in the shop's currency (whole coins, Real Money in tenths). 0 takes it off sale.</summary>
    public static bool SetPrice(string id, int itemId, float price)
    {
        if (itemId <= 1 || float.IsNaN(price) || price < 0f || price > PlayerShopMath.MaxTill)
            return false;
        return Edit(id, r => r.Prices[itemId] = PlayerShopMath.Settle(price, r.RealMoney));
    }

    /// <summary>Goes back to the markup price for an item.</summary>
    public static bool ClearPrice(string id, int itemId) => Edit(id, r => r.Prices.Remove(itemId));

    internal static void Init(ObenMod framework)
    {
        _save = ModSaves.Register<PlayerShopSave>(framework, "player-shops");
    }

    internal static void Shutdown() => _save = null;

    internal static PlayerShopRecord Record(string id) => _save != null ? _save.Value.Find(id) : null;

    private static bool Writable() => _save != null && WorldState.InWorld && !SaveGate.IsBusy;

    private static bool Edit(string id, Action<PlayerShopRecord> change)
    {
        var record = Writable() ? Record(id) : null;
        if (record == null)
            return false;
        change(record);
        return true;
    }

    private static PlayerShopInfo Describe(PlayerShopRecord record) =>
        new PlayerShopInfo(record, PlayerShopShelves.Of(record).Count);
}
