namespace ObenFramework.Economy;

/// <summary>
/// A snapshot of one of the player's bank accounts (the game's <c>Money.BankAccountData</c>). Accounts belong
/// to the save, not to a scene, so they can be read anywhere; take a new snapshot after changing one.
/// </summary>
public sealed class BankAccountInfo
{
    internal BankAccountInfo(Money.BankAccountData data)
    {
        Bank = data.Bankname ?? "";
        Coins = data.OC;
        RealMoney = data.RM;
        Frozen = data.Frozen;
        Interest = data.Interest;
        DebtInterest = data.DebtInterest;
        DebtCoins = data.CurrentDebt.OC;
        DebtRealMoney = data.CurrentDebt.RM;
        MonthlyCoins = data.MonthlyDownpayment.OC;
        MonthlyRealMoney = data.MonthlyDownpayment.RM;
    }

    /// <summary>The bank's name, which is also the account's key (<c>Stalburg Bank</c>).</summary>
    public string Bank { get; }

    /// <summary>Open Sewer Coin in the account.</summary>
    public int Coins { get; }

    /// <summary>Real Money in the account, to two decimals.</summary>
    public float RealMoney { get; }

    /// <summary>A frozen account can't be used until it is unfrozen (the lawyer can do that, see <see cref="LegalServices"/>).</summary>
    public bool Frozen { get; }

    /// <summary>Weekly interest on savings, 0.015 being 1.5%. It is only paid with the BankInterest fix of Unofficial ObenPatches.</summary>
    public float Interest { get; }

    /// <summary>Weekly interest on the debt.</summary>
    public float DebtInterest { get; }

    /// <summary>Open Sewer Coin owed on loans.</summary>
    public int DebtCoins { get; }

    /// <summary>Real Money owed on loans.</summary>
    public float DebtRealMoney { get; }

    /// <summary>What a monthly loan invoice asks for in coins, before the difficulty splits it into payments.</summary>
    public int MonthlyCoins { get; }

    /// <summary>What a monthly loan invoice asks for in Real Money.</summary>
    public float MonthlyRealMoney { get; }

    /// <summary>True when anything is owed.</summary>
    public bool HasDebt => DebtCoins > 0 || DebtRealMoney > 0f;

    public override string ToString() => $"{Bank}: {Coins} OC, {RealMoney} RM{(Frozen ? " (frozen)" : "")}";
}
