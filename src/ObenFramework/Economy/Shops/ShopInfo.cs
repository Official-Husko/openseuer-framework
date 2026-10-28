using System;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Economy;

/// <summary>The ways the player can buy from or sell to something in the game.</summary>
public enum ShopKind
{
    /// <summary>An NPC trader: items move between the player's inventory and the trader's storage (<c>Trade</c>).</summary>
    Trader,

    /// <summary>A supermarket checkout: goods ride a conveyor and the cashier scans them (<c>MarketShop</c>).</summary>
    Market,

    /// <summary>A furniture shop (<c>FurnitureShop</c>), priced as a percentage of the furniture's value.</summary>
    Furniture,

    /// <summary>A train car shop (<c>TrainShop</c>).</summary>
    Train,

    /// <summary>A vending machine with a fixed list of stocked items.</summary>
    Vending,
}

/// <summary>
/// One shop of the loaded scene: a trader, checkout, furniture or train shop, or a vending machine.
/// The object is cached per scene, so the same shop always gives the same <see cref="ShopInfo"/>.
/// </summary>
public sealed class ShopInfo
{
    internal ShopInfo(Component component, ShopKind kind, string name, ObjectAddress address)
    {
        Component = component;
        Kind = kind;
        Name = name;
        Address = address;
        Id = address.ToString();
    }

    /// <summary>The game component (<c>Trade</c>, <c>VendingMachine</c>, <c>MarketShop</c> or a <c>BaseShop</c>). Unity-null once the scene is gone.</summary>
    public Component Component { get; }

    public ShopKind Kind { get; }

    /// <summary>The title the game shows, or the object's name when it has none. Not unique: many vending machines share one.</summary>
    public string Name { get; }

    /// <summary>A stable address that survives restarts, see <see cref="ObjectAddress"/>.</summary>
    public ObjectAddress Address { get; }

    /// <summary>The address as text. Pass it back to <see cref="Shops.Find"/> or the stock calls to name exactly this shop.</summary>
    public string Id { get; }

    /// <summary>The game's GUID of the shop, or null when it has none.</summary>
    public string Guid => Address.Guid;

    /// <summary>False once the scene that held the shop is gone.</summary>
    public bool IsAlive => Component != null;

    /// <summary>
    /// True when <paramref name="key"/> names this shop: its full address text, its GUID, or its name
    /// (ignoring case, so a name matches every shop that carries it).
    /// </summary>
    public bool Matches(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return key == Id || (Guid != null && key == Guid) || string.Equals(key, Name, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => Kind + " " + Name;

    // Keys of the trader price factors, built once per shop so the price hooks don't concatenate per call.
    private string _buyKey;
    private string _sellKey;

    internal string FactorKey(bool playerBuys) =>
        playerBuys ? _buyKey ??= TraderFactors.KeyOf(Id, true) : _sellKey ??= TraderFactors.KeyOf(Id, false);
}
