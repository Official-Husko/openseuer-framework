// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>
/// The bank branches of the loaded scene (the game's <c>Bank</c>) and what the player can do at them:
/// open an account, deposit and withdraw, exchange currencies, take and repay loans. Everything goes through
/// the game's own bank methods, so supplies, corruption, notifications and the wallet behave as at the desk,
/// and nothing happens partly: a call that the bank would only half honour is refused instead. The player's
/// accounts themselves (any scene, online banking) are in <see cref="BankAccounts"/>, their history in
/// <see cref="BankLedger"/>. Call on the main thread.
/// </summary>
public static partial class Banks
{
    /// <summary>Every bank branch of the loaded scenes, switched-off ones included. A scan; not for every frame.</summary>
    public static List<BankInfo> All()
    {
        var list = new List<BankInfo>();
        foreach (var bank in Object.FindObjectsByType<Bank>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(Describe(bank));
        return list;
    }

    /// <summary>The branch named by its address, GUID, hierarchy path or bank name (the first branch of that name). Null when there is none.</summary>
    public static BankInfo Find(string key)
    {
        var bank = Resolve(key);
        return bank != null ? Describe(bank) : null;
    }

    /// <summary>The branch whose window the player has open right now, or null.</summary>
    public static BankInfo Open
    {
        get
        {
            var panel = BankPanel.instance;
            return panel != null && panel.activeBank != null ? Describe(panel.activeBank) : null;
        }
    }

    /// <summary>Opens the player's account at the branch's bank, as the clerk does. <see cref="BankResult.Ok"/> also when there already was one.</summary>
    public static BankResult OpenAccount(string bankKey)
    {
        var bank = Resolve(bankKey);
        var refusal = Check(bank, needAccount: false);
        if (refusal != BankResult.Ok)
            return refusal;
        return Guard("OpenAccount", () =>
        {
            bank.CreateAccount();
            return bank.PlayerHasAnAccount() ? BankResult.Ok : BankResult.Failed;
        });
    }

    /// <summary>
    /// Puts money from the wallet into the account. The bank's corruption may lose part of a deposit, as at the
    /// desk; read the account afterwards for what arrived. Refused at withdraw-only and exchange-only branches.
    /// </summary>
    public static BankResult Deposit(string bankKey, int coins, float realMoney)
    {
        var bank = Resolve(bankKey);
        var refusal = Check(bank, needAccount: true);
        if (refusal != BankResult.Ok)
            return refusal;
        if (coins < 0 || realMoney < 0f || (coins == 0 && realMoney <= 0f) || float.IsNaN(realMoney))
            return BankResult.Refused;
        if (bank.mode != BankPanel.Mode.Deposit && bank.mode != BankPanel.Mode.Both)
            return BankResult.NotOffered;
        if (!Wallet.CanAfford(coins, realMoney))
            return BankResult.Insufficient;

        // Real Money deposits are whole numbers at the desk too.
        int wholeRm = Mathf.FloorToInt(realMoney);
        if (coins == 0 && wholeRm == 0)
            return BankResult.TooSmall;
        var account = bank.GetPlayerAccount();
        if ((long)bank.TotalOCSupply + coins > int.MaxValue || !AccountMath.CoinsFit(account.OC, coins))
            return BankResult.Refused;
        return AtDesk(bank, "Deposit", () => bank.DepositWithdraw(coins, wholeRm));
    }

    /// <summary>Takes money out of the account into the wallet. Refused when the account or the bank's own supply can't cover all of it.</summary>
    public static BankResult Withdraw(string bankKey, int coins, float realMoney)
    {
        var bank = Resolve(bankKey);
        var refusal = Check(bank, needAccount: true);
        if (refusal != BankResult.Ok)
            return refusal;
        if (coins < 0 || realMoney < 0f || (coins == 0 && realMoney <= 0f) || float.IsNaN(realMoney))
            return BankResult.Refused;
        if (bank.mode == BankPanel.Mode.Exchange)
            return BankResult.NotOffered;

        float rm = Money.RoundRM(realMoney);
        if (coins == 0 && rm <= 0f)
            return BankResult.TooSmall;
        var account = bank.GetPlayerAccount();
        if (!AccountMath.Covers(false, account.OC, account.RM, coins) || !AccountMath.Covers(true, account.OC, account.RM, rm))
            return BankResult.Insufficient;
        if (coins > bank.TotalOCSupply || rm > bank.TotalRMSupply)
            return BankResult.BankShort;
        if (!WalletFits(coins, rm))
            return BankResult.Refused;
        return AtDesk(bank, "Withdraw", () => bank.DepositWithdraw(-coins, -rm));
    }

    internal static Bank Resolve(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        var found = ObjectResolver.Find<Bank>(key);
        if (found != null)
            return found;
        foreach (var bank in Object.FindObjectsByType<Bank>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.Equals(bank.BankName, key, StringComparison.OrdinalIgnoreCase))
                return bank;
        }
        return null;
    }

    internal static BankInfo Describe(Bank bank) =>
        new BankInfo(bank, Money.instance != null && !string.IsNullOrEmpty(bank.BankName) ? Money.instance.FindBankAccount(bank.BankName) : null);

    /// <summary>The common preconditions of a desk call.</summary>
    private static BankResult Check(Bank bank, bool needAccount)
    {
        if (!Wallet.CanChange || SaveGate.IsBusy)
            return BankResult.NotReady;
        if (bank == null || string.IsNullOrEmpty(bank.BankName))
            return BankResult.NoBank;
        if (!needAccount)
            return BankResult.Ok;
        var account = Money.instance.FindBankAccount(bank.BankName);
        if (account == null)
            return BankResult.NoAccount;
        return account.Frozen ? BankResult.Frozen : BankResult.Ok;
    }

    // Bank.DepositWithdraw reads the open bank window's branch for its rules, so the branch is put there
    // for the call and the window's own state is restored after.
    private static BankResult AtDesk(Bank bank, string what, Func<bool> call)
    {
        var panel = BankPanel.instance;
        if (panel == null)
            return BankResult.NotReady;
        var before = panel.activeBank;
        try
        {
            panel.activeBank = bank;
            return Guard(what, () => call() ? BankResult.Ok : BankResult.Failed);
        }
        finally
        {
            panel.activeBank = before;
        }
    }

    private static BankResult Guard(string what, Func<BankResult> call)
    {
        try
        {
            return call();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Banks." + what, e);
            return BankResult.Failed;
        }
    }

    private static bool WalletFits(float coins, float realMoney) =>
        (long)Wallet.Coins + (long)coins <= int.MaxValue &&
        (long)Wallet.ToTenths(Wallet.RealMoney) + Wallet.ToTenths(realMoney) <= int.MaxValue;
}
