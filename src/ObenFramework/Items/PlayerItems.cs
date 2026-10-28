using System.Collections.Generic;
using ObenFramework.Economy;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Items;

/// <summary>
/// Gives items to the player, takes them away and counts them. Everything here treats the grant as
/// coming from nowhere: the owner is -1 and crime checks are skipped, so mod rewards never count as
/// theft. Call these on the main thread while a world is loaded. <see cref="InventoryEvents"/> are raised
/// before a call returns, with <see cref="ChangeSource.Mod"/> as the source.
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
    /// For money ids this is the balance, rounded down. Liquids in bottles are not counted by their liquid's id.
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

    /// <summary>Empty slots in the inventory grid and the equipped bag.</summary>
    public static int FreeSlots => IsReady ? Inventory.instance.GetEmptySlotsAmount(false, true) : 0;

    /// <summary>Slots in the inventory grid and the equipped bag.</summary>
    public static int Capacity => IsReady ? PlayerSlots.Current(includeEquipment: false).Count : 0;

    /// <summary>Every stack the player carries (grid, bag, then clothing and hand slots) with its details.</summary>
    public static List<ItemStackInfo> Stacks()
    {
        var result = new List<ItemStackInfo>();
        var slots = PlayerSlots.Current();
        for (int i = 0; i < slots.Count; i++)
        {
            var stack = slots[i].itemStack;
            if (stack != null && stack.itemId != -1 && stack.itemAmount > 0)
                result.Add(ItemStackInfo.From(stack, i));
        }
        return result;
    }

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

        using (WorldChanges.EnterMod())
        {
            bool all = ItemOperations.AddItems(item, amount, -1, item.CloneMeta(false, amount, item), null, notify, ignoreCrime: true);
            InventoryWatch.Check();
            return all;
        }
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

        using (WorldChanges.EnterMod())
        {
            int left = ItemOperations.AddItemAndReturnRemaining(item, -1, null, item.CloneMeta(false, amount, item), amount);
            InventoryWatch.Check();
            return left;
        }
    }

    /// <summary>
    /// Takes exactly <paramref name="amount"/> items from the player, or nothing at all when they
    /// don't have that many. Stacks owned by other characters are taken too, without a theft check.
    /// </summary>
    /// <remarks>
    /// Liquids are refused: the game's remove call matches a bottle by the liquid in it and would take the
    /// whole bottle. Use <see cref="StackActions.TakeLiquid"/> for those. If the game removes a different
    /// number than asked, the difference is put back and the call returns false.
    /// </remarks>
    public static bool Take(int id, int amount = 1)
    {
        if (amount <= 0 || !IsReady)
            return false;
        if (id == CoinId)
            return Wallet.TryRemove(Currency.OC, amount);
        if (id == RealMoneyId)
            return Wallet.TryRemove(Currency.RM, amount);
        if (LiquidRisk(id))
        {
            ItemsLog.WarnOnce("take-liquid-" + id, $"PlayerItems.Take refused item {id}: it is a liquid or a bottle in the inventory holds it. Use StackActions.TakeLiquid.");
            return false;
        }

        int before = Count(id);
        if (before < amount)
            return false;

        using (WorldChanges.EnterMod())
        {
            // Inventory.RemoveItem removes one unit per call, from the first matching slot.
            for (int i = 0; i < amount; i++)
            {
                if (!Inventory.instance.RemoveItem(id, ignoreOwner: true, owner: -1, ignoreCrime: true))
                    break;
            }

            var result = TakeMath.Account(amount, before, Count(id), out int removed);
            if (result != TakeResult.Exact && removed > 0)
                PutBack(id, before, removed);
            InventoryWatch.Check();
            return result == TakeResult.Exact;
        }
    }

    private static bool LiquidRisk(int id)
    {
        var item = ItemLookup.ById(id);
        if (item != null && TakeMath.IsLiquid(item.Categories))
            return true;
        foreach (var slot in PlayerSlots.Current())
        {
            if (PlayerSlots.HoldsLiquid(slot, id))
                return true;
        }
        return false;
    }

    private static void PutBack(int id, int before, int removed)
    {
        var item = ItemLookup.ById(id);
        if (item != null)
            ItemOperations.AddItemAndReturnRemaining(item, -1, null, item.CloneMeta(false, removed, item), removed);
        int now = Count(id);
        if (now != before)
            ItemsLog.Error($"PlayerItems.Take couldn't put back {removed} of item {id}: the player has {now} instead of {before}.");
    }
}
