using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>Everything a price modifier may want to know about one price.</summary>
public readonly struct PriceQuery
{
    internal PriceQuery(ShopInfo shop, Item item, Furniture furniture, bool playerBuys, Currency currency, float price)
    {
        Shop = shop;
        Item = item;
        Furniture = furniture;
        PlayerBuys = playerBuys;
        Currency = currency;
        Price = price;
    }

    public ShopInfo Shop { get; }

    /// <summary>The item priced, or null in furniture and train shops.</summary>
    public Item Item { get; }

    /// <summary>The furniture priced, or null when the price is for an item.</summary>
    public Furniture Furniture { get; }

    /// <summary>True when the player pays this price, false when the shop pays the player.</summary>
    public bool PlayerBuys { get; }

    public Currency Currency { get; }

    /// <summary>The price of one unit so far: the game's own, then whatever earlier modifiers made of it.</summary>
    public float Price { get; }
}

/// <summary>A price modifier in its simple form. <paramref name="item"/> is null in furniture and train shops.</summary>
public delegate float PriceModifier(Item item, ShopInfo shop, float price);

/// <summary>
/// Changes what shops charge. Every price the game shows or charges goes through here: item prices at
/// traders, supermarket checkouts and vending machines, and furniture and train prices. Modifiers run in the
/// order they were registered (across all mods), each one receiving the previous one's result, and the final
/// price is rounded and kept at least the smallest amount the game can charge. Prices are per unit, in the
/// currency of the shop; a modifier that wants to treat currencies differently uses the
/// <see cref="PriceQuery"/> form. A modifier that throws is skipped for that price.
/// </summary>
public static class Prices
{
    private static readonly List<Entry> Entries = new List<Entry>();
    private static Entry[] _snapshot = Array.Empty<Entry>();
    private static bool _dirty;
    private static int _depth;

    /// <summary>True while at least one modifier is registered. The patches return at once otherwise.</summary>
    internal static bool Active => Entries.Count > 0;

    /// <summary>
    /// Adds a modifier until <paramref name="owner"/> unloads. A price of 0 or less is raised to the smallest
    /// price the game allows, so a mod cannot make an item free.
    /// </summary>
    public static IDisposable Register(ObenMod owner, PriceModifier modifier)
    {
        if (modifier == null)
            throw new ArgumentNullException(nameof(modifier));
        return Add(owner, q => modifier(q.Item, q.Shop, q.Price));
    }

    /// <summary>Like <see cref="Register(ObenMod, PriceModifier)"/>, with the full <see cref="PriceQuery"/>.</summary>
    public static IDisposable Register(ObenMod owner, Func<PriceQuery, float> modifier)
    {
        if (modifier == null)
            throw new ArgumentNullException(nameof(modifier));
        return Add(owner, modifier);
    }

    /// <summary>
    /// What the player would pay for one <paramref name="item"/> at a trader or vending machine right now,
    /// modifiers included, or null when the shop doesn't sell it.
    /// </summary>
    public static float? Of(ShopInfo shop, Item item) => ShopPricing.UnitPrice(shop, item, out _);

    /// <summary>
    /// What a trader pays the player for one <paramref name="item"/> right now, modifiers included, and in
    /// which currency; null for shops that don't buy (vending machines, checkouts). An item the player
    /// stole sells for less at the counter than this (the game's stolen price factor applies to owned stacks).
    /// </summary>
    public static float? OfSale(ShopInfo shop, Item item, out Currency currency) =>
        ShopPricing.UnitPrice(shop, item, out currency, playerBuys: false);

    private static IDisposable Add(ObenMod owner, Func<PriceQuery, float> modifier)
    {
        var entry = new Entry(owner.Info.Id, modifier);
        Entries.Add(entry);
        _dirty = true;
        var handle = Disposable.Create(() =>
        {
            if (Entries.Remove(entry))
                _dirty = true;
        });
        owner.Track(handle);
        return handle;
    }

    /// <summary>Runs the modifiers over a price. Returns the input when nothing changed it.</summary>
    internal static float Run(ShopInfo shop, Item item, Furniture furniture, bool playerBuys, Currency currency, float price)
    {
        // A modifier that asks the shop for a price would otherwise loop.
        if (_depth > 0)
            return price;

        if (_dirty)
        {
            _snapshot = Entries.ToArray();
            _dirty = false;
        }

        _depth++;
        try
        {
            float current = price;
            foreach (var entry in _snapshot)
            {
                try
                {
                    float next = entry.Modifier(new PriceQuery(shop, item, furniture, playerBuys, currency, current));
                    if (!float.IsNaN(next) && !float.IsInfinity(next))
                        current = next;
                }
                catch (Exception e)
                {
                    PricePatches.Fail(entry.OwnerId, e);
                }
            }
            return current;
        }
        finally
        {
            _depth--;
        }
    }

    internal static void Clear()
    {
        Entries.Clear();
        _snapshot = Array.Empty<Entry>();
        _dirty = false;
        _depth = 0;
    }

    private sealed class Entry
    {
        public readonly string OwnerId;
        public readonly Func<PriceQuery, float> Modifier;

        public Entry(string ownerId, Func<PriceQuery, float> modifier)
        {
            OwnerId = ownerId;
            Modifier = modifier;
        }
    }
}
