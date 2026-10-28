// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Economy;

/// <summary>What exchanging an amount at a branch would do right now, from <see cref="Banks.Quote"/>.</summary>
public readonly struct ExchangeQuote
{
    internal ExchangeQuote(bool toRealMoney, float taken, int credit, float rate, bool whole)
    {
        ToRealMoney = toRealMoney;
        Taken = taken;
        Credit = credit;
        Rate = rate;
        Whole = whole;
    }

    /// <summary>True for coins into Real Money, false for Real Money into coins.</summary>
    public bool ToRealMoney { get; }

    /// <summary>How much the bank would take from the wallet: the amount, or less when the wallet or the bank's supply is short.</summary>
    public float Taken { get; }

    /// <summary>What the player gets back, in whole units of the other currency.</summary>
    public int Credit { get; }

    /// <summary>The rate in force, lowered while the bank runs short of the currency it pays out.</summary>
    public float Rate { get; }

    /// <summary>True when the bank would take the full amount asked for.</summary>
    public bool Whole { get; }
}

public static partial class Banks
{
    /// <summary>
    /// What exchanging <paramref name="amount"/> would take and pay at this branch right now, worked out the way
    /// the game's exchange does it (not with the game's own quote method, which differs from it). Null when
    /// there is no such branch or no money loaded.
    /// </summary>
    /// <param name="toRealMoney">True to change coins into Real Money, false the other way.</param>
    public static ExchangeQuote? Quote(string bankKey, float amount, bool toRealMoney)
    {
        var bank = Resolve(bankKey);
        if (bank == null || Money.instance == null || !(amount > 0f))
            return null;

        float wallet = toRealMoney ? Wallet.Coins : Wallet.RealMoney;
        float supply = toRealMoney ? bank.TotalRMSupply : bank.TotalOCSupply;
        float rate = toRealMoney
            ? BankExchangeMath.RateToRm(bank.OS_RM_Ratio, bank.TotalRMSupply, bank.DailyAverageRmSpending)
            : BankExchangeMath.RateToCoins(bank.RM_OS_Ratio, bank.TotalOCSupply, bank.DailyAverageOcSpending);
        return new ExchangeQuote(toRealMoney, BankExchangeMath.Taken(amount, wallet, supply),
            BankExchangeMath.Credit(amount, wallet, supply, rate), rate, BankExchangeMath.WithinLimits(amount, wallet, supply));
    }

    /// <summary>
    /// Exchanges a whole amount of one currency into the other at the branch's rate, through the game's own
    /// exchange. Refused unless the branch has an exchange desk, the wallet and the bank's supply cover the full
    /// amount, and the exchange would pay at least 1 back (the game would otherwise take the money for nothing).
    /// </summary>
    public static BankResult Exchange(string bankKey, int amount, bool toRealMoney)
    {
        var bank = Resolve(bankKey);
        var refusal = Check(bank, needAccount: false);
        if (refusal != BankResult.Ok)
            return refusal;
        if (amount <= 0 || amount > AccountMath.MaxAmount)
            return BankResult.Refused;
        if (bank.mode != BankPanel.Mode.Exchange && bank.mode != BankPanel.Mode.Both)
            return BankResult.NotOffered;

        var quote = Quote(bankKey, amount, toRealMoney).GetValueOrDefault();
        if (amount > (toRealMoney ? Wallet.Coins : Wallet.RealMoney))
            return BankResult.Insufficient;
        if (!quote.Whole)
            return BankResult.BankShort;
        if (quote.Credit < 1)
            return BankResult.TooSmall;
        bool fits = toRealMoney ? WalletFits(0, quote.Credit) : WalletFits(quote.Credit, 0f);
        if (!fits || (toRealMoney && (long)bank.TotalOCSupply + amount > int.MaxValue))
            return BankResult.Refused;
        return Guard("Exchange", () => bank.Exchange(amount, toRealMoney) ? BankResult.Ok : BankResult.Failed);
    }
}
