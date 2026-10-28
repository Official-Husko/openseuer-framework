// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Clock;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenLoader;

namespace ObenFramework.Economy;

/// <summary>
/// The history of the player's bank accounts, which the game doesn't keep: every deposit, withdrawal, loan,
/// repayment, interest payment, freeze and so on, with the balance afterwards. It is kept per save (in the
/// framework's mod data, not the game's files) from the first time the framework sees a save, starting with
/// the balances the accounts had then; at most 500 entries per bank. Changes that no hook explains (another
/// mod writing the account directly) are recorded as <see cref="LedgerKind.Other"/>.
/// </summary>
public static class BankLedger
{
    private static readonly List<LedgerEntry> Fresh = new List<LedgerEntry>();
    private static ModSaveData<LedgerSave> _save;
    private static bool _reconciling;

    /// <summary>A line was added to an account's history. The entry is a copy.</summary>
    public static readonly WorldEvent<LedgerEntry> Added = new WorldEvent<LedgerEntry>(nameof(Added));

    /// <summary>The game day of the oldest entry, or -1 while there is none.</summary>
    public static int FirstDay => _save != null ? _save.Value.FirstDay : -1;

    /// <summary>
    /// The newest entries of one bank's account first, at most <paramref name="max"/>. Pass the smallest
    /// <see cref="LedgerEntry.Id"/> you got as <paramref name="beforeId"/> to read further back.
    /// </summary>
    public static List<LedgerEntry> Entries(string bank, int max = 50, int beforeId = 0)
    {
        Reconcile(LedgerKind.Other);
        return _save != null ? LedgerBook.Entries(_save.Value, bank, Math.Min(max, LedgerBook.CapPerBank), beforeId) : new List<LedgerEntry>();
    }

    internal static void Init(ObenMod framework)
    {
        _save = ModSaves.Register<LedgerSave>(framework, "bank-ledger");
        // The first look at a save's accounts writes down what they hold, so later entries add up from there.
        GameEvents.WhenWorldReady(framework, _ => Reconcile(LedgerKind.Other));
    }

    internal static void Shutdown() => _save = null;

    /// <summary>
    /// Compares every account with what the ledger last saw and writes the differences down as
    /// <paramref name="reason"/>. Cheap and allocation-free when nothing changed; the patches call it before
    /// (as <see cref="LedgerKind.Other"/>) and after each game method that touches accounts.
    /// </summary>
    internal static void Reconcile(LedgerKind reason)
    {
        var money = Money.instance;
        if (_save == null || _reconciling || money == null || money.bankAccounts == null || !Ready)
            return;

        _reconciling = true;
        try
        {
            var save = _save.Value;
            int day = GameClock.IsAvailable ? GameClock.Day : 0;
            for (int i = 0; i < money.bankAccounts.Count; i++)
            {
                var account = money.bankAccounts[i];
                if (account == null || string.IsNullOrEmpty(account.Bankname))
                    continue;
                var now = new AccountValues(account.Bankname, account.OC, account.RM, account.CurrentDebt.OC, account.CurrentDebt.RM, account.Frozen);
                LedgerBook.Track(save, now, reason, day, Fresh);
            }
            save.Baselined = true;
        }
        catch (Exception e)
        {
            WorldLog.Failed("Bank ledger", e);
        }
        finally
        {
            _reconciling = false;
        }
        Announce();
    }

    // Only once the save's money is in place; mid-load the accounts are a mix of two saves.
    private static bool Ready => WorldState.InWorld && !WorldState.IsLoadingSave && !SaveGate.IsLoading;

    private static void Announce()
    {
        if (Fresh.Count == 0)
            return;
        if (!Added.HasSubscribers)
        {
            Fresh.Clear();
            return;
        }
        var batch = Fresh.ToArray();
        Fresh.Clear();
        foreach (var entry in batch)
            Added.Raise(entry.Copy());
    }
}
