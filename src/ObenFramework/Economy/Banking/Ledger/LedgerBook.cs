// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Economy;

/// <summary>
/// The bookkeeping behind <see cref="BankLedger"/>, without Unity types: compares an account with what was
/// last seen of it and writes down the difference under the reason the caller knows. A change no hook
/// explained shows up as <see cref="LedgerKind.Other"/> the next time any reconcile runs.
/// </summary>
internal static class LedgerBook
{
    /// <summary>Entries kept per bank; the oldest go first.</summary>
    public const int CapPerBank = 500;

    // Real Money balances carry float noise from the game's rounding.
    private const float Tolerance = 0.0001f;

    /// <summary>
    /// Records what changed in <paramref name="now"/> since the ledger last saw it. Returns how many entries
    /// were written (0 when nothing changed; 2 when money moved and the freeze flag flipped at once).
    /// </summary>
    /// <param name="reason">What the caller knows happened. <see cref="LedgerKind.Deposit"/> and <see cref="LedgerKind.Withdrawal"/> are told apart by the sign.</param>
    /// <param name="added">Receives the new entries; may be null.</param>
    public static int Track(LedgerSave save, in AccountValues now, LedgerKind reason, int day, List<LedgerEntry> added)
    {
        var state = Find(save, now.Bank);
        bool isNew = state == null;
        if (isNew)
        {
            state = new LedgerAccountState { Bank = now.Bank };
            save.Accounts.Add(state);
        }

        int coins = now.Coins - state.Coins;
        float rm = now.RealMoney - state.RealMoney;
        int debtCoins = now.DebtCoins - state.DebtCoins;
        float debtRm = now.DebtRealMoney - state.DebtRealMoney;
        bool moved = coins != 0 || debtCoins != 0 || Math.Abs(rm) > Tolerance || Math.Abs(debtRm) > Tolerance;
        bool flipped = !isNew && now.Frozen != state.Frozen;
        if (!isNew && !moved && !flipped)
            return 0;

        LedgerKind kind;
        if (isNew)
            kind = save.Baselined ? Opened(reason) : LedgerKind.Opening;
        else if (moved)
            kind = KindOf(reason, coins, rm);
        else
            kind = now.Frozen ? LedgerKind.Frozen : LedgerKind.Unfrozen;

        Copy(now, state);
        int count = 0;
        Append(save, New(now, kind, day, coins, rm, debtCoins, debtRm), added);
        count++;
        if (moved && flipped)
        {
            Append(save, New(now, now.Frozen ? LedgerKind.Frozen : LedgerKind.Unfrozen, day, 0, 0f, 0, 0f), added);
            count++;
        }
        return count;
    }

    /// <summary>The newest entries of one bank first, at most <paramref name="max"/>, only ids below <paramref name="beforeId"/> when it is above 0.</summary>
    public static List<LedgerEntry> Entries(LedgerSave save, string bank, int max, int beforeId)
    {
        var list = new List<LedgerEntry>();
        if (string.IsNullOrEmpty(bank) || max <= 0)
            return list;
        for (int i = save.Entries.Count - 1; i >= 0 && list.Count < max; i--)
        {
            var entry = save.Entries[i];
            if (entry.Bank == bank && (beforeId <= 0 || entry.Id < beforeId))
                list.Add(entry.Copy());
        }
        return list;
    }

    /// <summary>Forgets an account that no longer exists, so a new account of that name starts as opened.</summary>
    public static bool Forget(LedgerSave save, string bank) =>
        save.Accounts.RemoveAll(a => a.Bank == bank) > 0;

    private static LedgerKind Opened(LedgerKind reason) =>
        reason == LedgerKind.Other || reason == LedgerKind.Opening ? LedgerKind.Opened : reason;

    private static LedgerKind KindOf(LedgerKind reason, int coins, float rm)
    {
        if (reason != LedgerKind.Deposit && reason != LedgerKind.Withdrawal)
            return reason;
        bool inward = coins != 0 ? coins > 0 : rm > 0f;
        return inward ? LedgerKind.Deposit : LedgerKind.Withdrawal;
    }

    private static LedgerAccountState Find(LedgerSave save, string bank)
    {
        foreach (var state in save.Accounts)
        {
            if (state.Bank == bank)
                return state;
        }
        return null;
    }

    private static void Copy(in AccountValues now, LedgerAccountState state)
    {
        state.Coins = now.Coins;
        state.RealMoney = now.RealMoney;
        state.DebtCoins = now.DebtCoins;
        state.DebtRealMoney = now.DebtRealMoney;
        state.Frozen = now.Frozen;
    }

    private static LedgerEntry New(in AccountValues now, LedgerKind kind, int day, int coins, float rm, int debtCoins, float debtRm) =>
        new LedgerEntry
        {
            Day = day,
            Bank = now.Bank,
            Kind = kind,
            Coins = coins,
            RealMoney = (float)Math.Round(rm, 2),
            DebtCoins = debtCoins,
            DebtRealMoney = (float)Math.Round(debtRm, 2),
            CoinsAfter = now.Coins,
            RealMoneyAfter = now.RealMoney,
        };

    private static void Append(LedgerSave save, LedgerEntry entry, List<LedgerEntry> added)
    {
        entry.Id = save.NextId++;
        if (save.FirstDay < 0)
            save.FirstDay = entry.Day;
        save.Entries.Add(entry);
        Trim(save, entry.Bank);
        added?.Add(entry);
    }

    private static void Trim(LedgerSave save, string bank)
    {
        int count = 0;
        int oldest = -1;
        for (int i = 0; i < save.Entries.Count; i++)
        {
            if (save.Entries[i].Bank != bank)
                continue;
            if (oldest < 0)
                oldest = i;
            count++;
        }
        if (count > CapPerBank)
            save.Entries.RemoveAt(oldest);
    }
}
