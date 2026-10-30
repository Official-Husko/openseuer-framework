namespace ObenFramework.Economy;

/// <summary>How a purchase or sale made from code (<see cref="Traders.Buy"/>, <see cref="FurnitureShops.Buy"/> and their sells) went.</summary>
public enum TradeResult
{
    /// <summary>Done: the goods and the money moved.</summary>
    Ok,

    /// <summary>No world, inventory or building system to trade with right now.</summary>
    NotReady,

    /// <summary>A save is being written or loaded, or another trade from code is running.</summary>
    Busy,

    /// <summary>A count below 1, a liquid, money or an item id that doesn't exist.</summary>
    BadInput,

    /// <summary>No shop of that kind answers to the key in the loaded scenes.</summary>
    UnknownShop,

    /// <summary>The trader is closed.</summary>
    Closed,

    /// <summary>A shop with its own rules the game handles in its window only: certificate traders, train shops, furniture that needs a picture.</summary>
    Unsupported,

    /// <summary>The shop doesn't have that many on offer at its plain price.</summary>
    OutOfStock,

    /// <summary>The player doesn't have that many (unplaced furniture, or items the shop could take).</summary>
    NotEnoughItems,

    /// <summary>The shop doesn't take this: stolen, not in its categories, or a piece it doesn't buy.</summary>
    NotAccepted,

    /// <summary>The player can't pay.</summary>
    CannotAfford,

    /// <summary>The shop hasn't the money to pay the player.</summary>
    ShopCannotAfford,

    /// <summary>The goods don't fit: the player's inventory, or the trader's storage.</summary>
    NoRoom,

    /// <summary>Something went wrong on the way; everything was put back as it was.</summary>
    Failed,
}
