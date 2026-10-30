using System;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// The player's money. The game has two currencies, Open Sewer Coin (<see cref="Currency.OC"/>, whole
/// numbers) and Real Money (<see cref="Currency.RM"/>, kept in tenths: 12.3 is fine, 12.34 rounds to 12.3).
/// Every change goes through the game's own <c>Money.AddMoney</c> / <c>RemoveMoney</c>, so the HUD,
/// dialogue variables and sounds stay in sync. Call these on the main thread.
/// </summary>
public static class Wallet
{
    /// <summary>True when the framework's hooks on the game's money calls are in place, so <see cref="Changed"/> is raised. Reading and changing money works either way.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Economy");

    /// <summary>
    /// A balance changed through the game's <c>Money.AddMoney</c> or <c>RemoveMoney</c>, whoever called it: a
    /// shop, a bank, a quest, a mod. Raised once per currency that changed, after the change.
    /// </summary>
    public static readonly WorldEvent<MoneyChange> Changed = new WorldEvent<MoneyChange>(nameof(Changed));

    /// <summary>True when balances can be read.</summary>
    public static bool CanRead => Money.instance != null;

    /// <summary>True when money can be added or removed. The game needs its interaction UI for that, which only exists in a world.</summary>
    public static bool CanChange => Money.instance != null && InteractObjects.instance != null;

    public static int Coins => CanRead ? Money.instance.OCMoneyAmount : 0;

    public static float RealMoney => CanRead ? Money.instance.RMoneyAmount : 0f;

    public static float Get(Currency currency) => currency == Currency.RM ? RealMoney : Coins;

    /// <summary>Real Money as a whole number of tenths, the unit the game stores. Use this to compare amounts without float noise.</summary>
    public static int ToTenths(float realMoney) => Mathf.RoundToInt(realMoney * 10f);

    /// <summary>
    /// Real Money in coins at the game's fixed rate (the one prices and shops use, cut down to whole coins).
    /// Banks exchange at their own, supply-dependent rate: see <c>Banks.Quote</c>. 0 without a game.
    /// </summary>
    public static int RmToOc(float realMoney) => CanRead ? Money.instance.RmToOcConvert(realMoney) : 0;

    /// <summary>Coins in Real Money at the game's fixed rate, rounded the way the game rounds RM. 0 without a game.</summary>
    public static float OcToRm(float coins) => CanRead ? Money.instance.OcToRmConvert(coins) : 0f;

    public static bool CanAfford(Currency currency, float amount)
    {
        if (!CanRead)
            return false;
        if (currency == Currency.RM)
            return ToTenths(RealMoney) >= ToTenths(amount);
        return Coins >= Mathf.RoundToInt(amount);
    }

    /// <summary>True when the player has both amounts at once.</summary>
    public static bool CanAfford(float coins, float realMoney) =>
        CanAfford(Currency.OC, coins) && CanAfford(Currency.RM, realMoney);

    /// <summary>
    /// Adds money. A negative amount removes it instead, down to zero at most (the game never lets a
    /// balance go negative). Returns false when nothing could be changed, also while a save is written or
    /// loaded (the change would be half saved, or overwritten).
    /// </summary>
    public static bool Add(Currency currency, float amount)
    {
        if (!CanChange || SaveGate.IsBusy)
            return false;
        if (amount < 0f)
            return RemoveUpTo(currency, -amount);

        float value = Normalize(currency, amount);
        if (value == 0f)
            return true;
        if (WouldOverflow(currency, value))
            return false;
        Money.instance.AddMoney(value, currency);
        return true;
    }

    /// <summary>Removes exactly this much if the player can afford it. Otherwise nothing changes and it returns false, as while a save is written or loaded.</summary>
    public static bool TryRemove(Currency currency, float amount)
    {
        if (!CanChange || SaveGate.IsBusy || amount < 0f || !CanAfford(currency, amount))
            return false;

        float value = Normalize(currency, amount);
        if (value > 0f)
            Money.instance.RemoveMoney(value, currency);
        return true;
    }

    /// <summary>Removes both amounts, or neither when the player can't afford them together.</summary>
    public static bool TryRemove(float coins, float realMoney)
    {
        if (!CanChange || SaveGate.IsBusy || coins < 0f || realMoney < 0f || !CanAfford(coins, realMoney))
            return false;
        return TryRemove(Currency.OC, coins) && TryRemove(Currency.RM, realMoney);
    }

    private static bool RemoveUpTo(Currency currency, float amount)
    {
        float value = Math.Min(Normalize(currency, amount), Get(currency));
        if (value > 0f)
            Money.instance.RemoveMoney(value, currency);
        return true;
    }

    // OC is whole numbers, rounded to the nearest; RM is rounded to tenths.
    private static float Normalize(Currency currency, float amount) =>
        currency == Currency.RM ? ToTenths(amount) / 10f : Mathf.RoundToInt(amount);

    private static bool WouldOverflow(Currency currency, float value) =>
        currency == Currency.RM
            ? (long)ToTenths(RealMoney) + ToTenths(value) > int.MaxValue
            : (long)Coins + (int)value > int.MaxValue;
}
