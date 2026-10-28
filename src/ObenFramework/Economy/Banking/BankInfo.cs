using ObenFramework.World.Addressing;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// A snapshot of one bank branch in the loaded scene (the game's <c>Bank</c>): its services, money supply,
/// rates and loan terms, and the player's account there. Several branches can share a bank name and so share
/// one account. Take a new snapshot after a change.
/// </summary>
public sealed class BankInfo
{
    internal BankInfo(Bank bank, Money.BankAccountData account)
    {
        Bank = bank;
        Id = ObjectAddress.Of(bank).ToString();
        Guid = string.IsNullOrEmpty(bank.GUID) ? null : bank.GUID;
        Name = bank.BankName ?? "";
        Scene = bank.gameObject.scene.name;
        Services = bank.mode;
        Currencies = bank.currencies;
        CoinSupply = bank.TotalOCSupply;
        RealMoneySupply = bank.TotalRMSupply;
        Interest = bank.Interest;
        Corruption = bank.Corruption;
        CoinToRealMoney = bank.OS_RM_Ratio;
        RealMoneyToCoins = bank.RM_OS_Ratio;
        LoanCurrency = bank.LoanCurrency;
        DebtInterest = bank.DebtInterest;
        PaymentMonths = bank.PaymentTimemonths;
        MaxLoanCoins = bank.Max_OC_Loan;
        MaxLoanRealMoney = bank.Max_RM_Loan;
        LoanWithoutAccount = bank.TakeLoanDirectly;
        Account = account != null && !string.IsNullOrEmpty(account.Bankname) ? new BankAccountInfo(account) : null;
    }

    /// <summary>The game component. Unity-null once the scene is gone.</summary>
    public Bank Bank { get; }

    /// <summary>The branch's address (<c>scene|Bank|guid|path</c>); <see cref="Banks.Find"/> takes it back.</summary>
    public string Id { get; }

    public string Guid { get; }

    /// <summary>The bank's name, which keys the player's account.</summary>
    public string Name { get; }

    public string Scene { get; }

    /// <summary>What the branch's window offers: exchange, deposits, both, or withdrawals only.</summary>
    public BankPanel.Mode Services { get; }

    public Bank.Currencies Currencies { get; }

    /// <summary>Coins the bank holds. Withdrawals and exchanges into coins are capped by it.</summary>
    public int CoinSupply { get; }

    /// <summary>Real Money the bank holds.</summary>
    public float RealMoneySupply { get; }

    /// <summary>Weekly interest the bank gives new accounts.</summary>
    public float Interest { get; }

    /// <summary>Up to this share of a deposit goes missing (0.1 is up to 10%).</summary>
    public float Corruption { get; }

    /// <summary>Real Money per coin at full supply.</summary>
    public float CoinToRealMoney { get; }

    /// <summary>Coins per Real Money at full supply.</summary>
    public float RealMoneyToCoins { get; }

    public Currency LoanCurrency { get; }

    /// <summary>Weekly interest on loans.</summary>
    public float DebtInterest { get; }

    /// <summary>Months a loan is paid back over.</summary>
    public int PaymentMonths { get; }

    public int MaxLoanCoins { get; }

    public int MaxLoanRealMoney { get; }

    /// <summary>True when a loan pays out to the wallet and needs no account first (one is opened on the way).</summary>
    public bool LoanWithoutAccount { get; }

    /// <summary>The player's account at this bank, or null when there is none.</summary>
    public BankAccountInfo Account { get; }

    public bool HasAccount => Account != null;

    public bool OffersExchange => Services == BankPanel.Mode.Exchange || Services == BankPanel.Mode.Both;

    public bool OffersDeposits => Services == BankPanel.Mode.Deposit || Services == BankPanel.Mode.Both;

    /// <summary>False once the scene that held the branch is gone.</summary>
    public bool IsAlive => Bank != null;

    public override string ToString() => $"{Name} ({Services})";
}
