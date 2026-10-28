using ObenFramework.Economy;
using Currency = Money.Currency;

namespace ObenFramework.Items;

/// <summary>
/// Gives items to the player, takes them away and counts them. Everything here treats the grant as
/// coming from nowhere: the owner is -1 and crime checks are skipped, so mod rewards never count as
/// theft. Call these on the main thread while a world is loaded.
/// </summary>
/// <remarks>
/// Items 0 (Open Sewer Coin) and 1 (Real Money) are not stacks in the game. They are the two currencies,
/// so giving, taking and counting them is redirected to the <see cref="Wallet"/> and uses whole units.
/// </remarks>
public static class PlayerItems
{
    public const int CoinId = 0;
    public const int RealMoneyId = 1;

    public static bool IsMoney(int id) => id == CoinId || id == RealMoneyId;

    /// <summary>True when the inventory and item database exist, so the calls below can work.</summary>
    public static bool IsReady => Inventory.instance != null && ItemDatabase.instance != null;

    /// <summary>
    /// How many of an item the player has in the inventory grid, equipment slots and the equipped bag.
    /// For money ids this is the balance, rounded down.
    /// </summary>
    public static int Count(int id)
    {
        if (id == CoinId)
            return Wallet.Coins;
        if (id == RealMoneyId)
            return (int)Wallet.RealMoney;
        return IsReady ? Inventory.instance.GetItemAmountInInventory(id) : 0;
    }

    public static bool Has(int id, int amount = 1) => amount <= 0 || Count(id) >= amount;

    /// <summary>
    /// Gives the player items. The game fills the inventory grid first; whatever doesn't fit is dropped on
    /// the ground at the player's feet, not lost. Returns true when everything fit and false when
    /// anything was dropped (or the id/amount was invalid). Use <see cref="GiveWithoutDropping"/> to
    /// keep control of the overflow.
    /// </summary>
    public static bool Give(int id, int amount = 1, bool notify = false) =>
        Give(ItemLookup.ById(id), amount, notify);

    /// <inheritdoc cref="Give(int, int, bool)"/>
    public static bool Give(Item item, int amount = 1, bool notify = false)
    {
        if (item == null || amount <= 0 || !IsReady)
            return false;
        if (IsMoney(item.ID))
            return Wallet.Add(item.ID == CoinId ? Currency.OC : Currency.RM, amount);

        return ItemOperations.AddItems(item, amount, -1, item.CloneMeta(false, amount, item), null, notify, ignoreCrime: true);
    }

    /// <summary>
    /// Gives the player as many items as fit (inventory grid, then equipment slots, then the equipped
    /// bag) and drops nothing. Returns how many did not fit, so 0 means all of them were added.
    /// </summary>
    public static int GiveWithoutDropping(Item item, int amount = 1)
    {
        if (item == null || amount <= 0 || !IsReady)
            return amount > 0 ? amount : 0;
        if (IsMoney(item.ID))
            return Give(item, amount) ? 0 : amount;

        return ItemOperations.AddItemAndReturnRemaining(item, -1, null, item.CloneMeta(false, amount, item), amount);
    }

    /// <summary>
    /// Takes exactly <paramref name="amount"/> items from the player, or nothing at all when they
    /// don't have that many. Stacks owned by other characters are taken too, without a theft check.
    /// </summary>
    public static bool Take(int id, int amount = 1)
    {
        if (amount <= 0 || !IsReady)
            return false;
        if (id == CoinId)
            return Wallet.TryRemove(Currency.OC, amount);
        if (id == RealMoneyId)
            return Wallet.TryRemove(Currency.RM, amount);
        if (Count(id) < amount)
            return false;

        // Inventory.RemoveItem removes one unit per call, from the first matching slot.
        for (int i = 0; i < amount; i++)
        {
            if (!Inventory.instance.RemoveItem(id, ignoreOwner: true, owner: -1, ignoreCrime: true))
                return false;
        }
        return true;
    }
}
