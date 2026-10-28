// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ObenFramework.Economy;

/// <summary>What moved an account, as the <see cref="BankLedger"/> records it.</summary>
public enum LedgerKind
{
    /// <summary>The balance the account had when the ledger first saw it.</summary>
    Opening,
    /// <summary>The account was opened.</summary>
    Opened,
    Deposit,
    Withdrawal,
    /// <summary>A loan was taken (the debt grew, and the account too at banks that pay loans into it).</summary>
    Loan,
    /// <summary>Debt paid back at the desk, or from the account.</summary>
    Repayment,
    /// <summary>The game or a quest put money into the account.</summary>
    Credit,
    /// <summary>Weekly interest on savings or debt.</summary>
    Interest,
    /// <summary>A monthly loan invoice was paid.</summary>
    DebtPayment,
    /// <summary>A forged invoice paid out into the account.</summary>
    ScamPayout,
    /// <summary>The lawyer unfroze the account.</summary>
    Legal,
    /// <summary>A subscription took or paid its fee.</summary>
    Subscription,
    /// <summary>A mod charged the account (online purchases, card payments).</summary>
    Purchase,
    /// <summary>A mod paid money into the account.</summary>
    Refund,
    /// <summary>An invoice was paid from the account.</summary>
    Invoice,
    /// <summary>Money was taken out and sent by post.</summary>
    CashByPost,
    Frozen,
    Unfrozen,
    /// <summary>Something no hook saw changed the account (another mod, a console command).</summary>
    Other,
}

/// <summary>One line of an account's history.</summary>
public sealed class LedgerEntry
{
    /// <summary>Increasing number across all accounts; use it to page with <see cref="BankLedger.Entries"/>.</summary>
    public int Id;

    /// <summary>The game day (the day counter, not the date) it happened on.</summary>
    public int Day;

    public string Bank;

    [JsonConverter(typeof(StringEnumConverter))]
    public LedgerKind Kind;

    /// <summary>Change of the coin balance.</summary>
    public int Coins;

    /// <summary>Change of the Real Money balance.</summary>
    public float RealMoney;

    /// <summary>Change of the coin debt.</summary>
    public int DebtCoins;

    /// <summary>Change of the Real Money debt.</summary>
    public float DebtRealMoney;

    /// <summary>The coin balance afterwards.</summary>
    public int CoinsAfter;

    /// <summary>The Real Money balance afterwards.</summary>
    public float RealMoneyAfter;

    internal LedgerEntry Copy() => (LedgerEntry)MemberwiseClone();
}

/// <summary>The values of an account the ledger compares.</summary>
internal readonly struct AccountValues
{
    public AccountValues(string bank, int coins, float realMoney, int debtCoins, float debtRealMoney, bool frozen)
    {
        Bank = bank;
        Coins = coins;
        RealMoney = realMoney;
        DebtCoins = debtCoins;
        DebtRealMoney = debtRealMoney;
        Frozen = frozen;
    }

    public string Bank { get; }
    public int Coins { get; }
    public float RealMoney { get; }
    public int DebtCoins { get; }
    public float DebtRealMoney { get; }
    public bool Frozen { get; }
}

/// <summary>What the ledger last saw of one account.</summary>
internal sealed class LedgerAccountState
{
    public string Bank = "";
    public int Coins;
    public float RealMoney;
    public int DebtCoins;
    public float DebtRealMoney;
    public bool Frozen;
}

/// <summary>The ledger as stored with the save (<c>mods/ObenFramework.bank-ledger.json</c>).</summary>
internal sealed class LedgerSave
{
    public int NextId = 1;

    /// <summary>The day of the first entry, or -1 before there is one.</summary>
    public int FirstDay = -1;

    /// <summary>True once the accounts that existed when the ledger started were written down as openings.</summary>
    public bool Baselined;

    public List<LedgerAccountState> Accounts = new List<LedgerAccountState>();

    public List<LedgerEntry> Entries = new List<LedgerEntry>();
}
