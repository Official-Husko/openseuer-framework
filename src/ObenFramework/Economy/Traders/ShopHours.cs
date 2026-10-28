// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.World;

namespace ObenFramework.Economy;

/// <summary>One day of a trader's opening hours.</summary>
public readonly struct ShopDay
{
    internal ShopDay(DayOfWeek day, int opens, int closes)
    {
        Day = day;
        Opens = opens;
        Closes = closes;
    }

    public DayOfWeek Day { get; }

    /// <summary>The hour it opens (0 to 24).</summary>
    public int Opens { get; }

    /// <summary>The hour it closes; before <see cref="Opens"/> means it closes after midnight.</summary>
    public int Closes { get; }

    /// <summary>False for a day it doesn't open at all.</summary>
    public bool OpensThisDay => Opens != Closes;

    /// <summary>True when it is open at <paramref name="hour"/> of this day.</summary>
    public bool IsOpenAt(float hour) => ShopHoursMath.IsOpenAt(Opens, Closes, hour);

    public override string ToString() => OpensThisDay ? $"{Day} {Opens:00}-{Closes:00}" : $"{Day} closed";
}

/// <summary>
/// Opening hours. NPC traders open and close by their hours (checked every game minute, and only when the
/// player isn't standing right there); vending machines, furniture shops and checkouts follow no clock
/// (a checkout can be locked, see <see cref="MarketShops"/>). <see cref="Opened"/> and <see cref="Closed"/>
/// report traders opening and closing. Call on the main thread.
/// </summary>
public static class ShopHours
{
    /// <summary>A trader opened for the day.</summary>
    public static readonly WorldEvent<ShopInfo> Opened = new WorldEvent<ShopInfo>(nameof(Opened));

    /// <summary>A trader closed.</summary>
    public static readonly WorldEvent<ShopInfo> Closed = new WorldEvent<ShopInfo>(nameof(Closed));

    /// <summary>A trader's hours, Monday first, or null when <paramref name="key"/> names no trader.</summary>
    public static ShopDay[] Hours(string key)
    {
        var trade = TradeOf(key);
        var times = trade != null ? trade.openingTimes : null;
        if (times == null)
            return null;
        return new[]
        {
            new ShopDay(DayOfWeek.Monday, times.opensOnMonday, times.closesOnMonday),
            new ShopDay(DayOfWeek.Tuesday, times.opensOnTuesday, times.closesOnTuesday),
            new ShopDay(DayOfWeek.Wednesday, times.opensOnWednesday, times.closesOnWednesday),
            new ShopDay(DayOfWeek.Thursday, times.opensOnThursday, times.closesOnThursday),
            new ShopDay(DayOfWeek.Friday, times.opensOnFriday, times.closesOnFriday),
            new ShopDay(DayOfWeek.Saturday, times.opensOnSaturday, times.closesOnSaturday),
            new ShopDay(DayOfWeek.Sunday, times.opensOnSunday, times.closesOnSunday),
        };
    }

    /// <summary>
    /// True when the shop sells right now: a trader that is open, a checkout that isn't locked, any vending
    /// machine or furniture shop. False for an unknown shop.
    /// </summary>
    public static bool IsOpen(string key)
    {
        var shop = Shops.Find(key);
        if (shop == null || !shop.IsAlive)
            return false;
        return shop.Component switch
        {
            Trade trade => trade.open,
            MarketShop market => market.marketLock == null || !market.marketLock.Locked,
            _ => true,
        };
    }

    /// <summary>
    /// True when a trader's hours say it should be open now. It can differ from <see cref="IsOpen"/> for a
    /// minute, while the player stands at the counter, or for traders a quest opens and closes by hand.
    /// </summary>
    public static bool IsScheduledOpen(string key)
    {
        var trade = TradeOf(key);
        if (trade == null || trade.openingTimes == null || TimeOfDayAzure.instance == null)
            return false;
        return trade.openingTimes.IsOpen();
    }

    private static Trade TradeOf(string key)
    {
        foreach (var shop in Shops.FindAll(key))
        {
            if (shop.Component is Trade trade && trade != null)
                return trade;
        }
        return null;
    }

    internal static void Changed(Trade trade, bool open)
    {
        var e = open ? Opened : Closed;
        if (!e.HasSubscribers || !WorldChanges.Active)
            return;
        var shop = Shops.Of(trade);
        if (shop != null)
            e.Raise(shop);
    }
}
