using ObenFramework.Economy;

namespace ObenCore.Tests.Economy;

public class BankExchangeMathTests
{
    [Fact]
    public void FullSupplyUsesTheBankRatio()
    {
        Assert.Equal(0.06f, BankExchangeMath.RateToRm(0.06f, 12500f, 1000));
        Assert.Equal(9.8f, BankExchangeMath.RateToCoins(9.8f, 5000, 1000));
    }

    [Fact]
    public void ShortRealMoneyLowersTheRateInProportion()
    {
        Assert.Equal(0.03f, BankExchangeMath.RateToRm(0.06f, 500f, 1000), 5);
    }

    [Fact]
    public void ShortCoinsPayNothingBecauseTheGameDividesWholeNumbers()
    {
        // TotalOCSupply / DailyAverageOcSpending is an integer division in the game: 999 / 1000 == 0.
        Assert.Equal(0f, BankExchangeMath.RateToCoins(9.8f, 999, 1000));
    }

    [Fact]
    public void ZeroSpendingNeverDividesByZero()
    {
        Assert.Equal(0.06f, BankExchangeMath.RateToRm(0.06f, 0f, 0));
        Assert.Equal(9.8f, BankExchangeMath.RateToCoins(9.8f, 0, 0));
    }

    [Fact]
    public void TakenIsCappedByWalletThenSupply()
    {
        Assert.Equal(5f, BankExchangeMath.Taken(10f, 5f, 100f));
        Assert.Equal(3f, BankExchangeMath.Taken(10f, 100f, 3f));
        Assert.Equal(0f, BankExchangeMath.Taken(10f, -1f, 3f));
    }

    [Fact]
    public void CreditIsTruncated()
    {
        Assert.Equal(0, BankExchangeMath.Credit(1f, 1f, 100f, 0.5f));
        Assert.Equal(2, BankExchangeMath.Credit(10f, 5f, 100f, 0.5f));
        Assert.Equal(2, BankExchangeMath.Credit(7f, 100f, 100f, 0.3f));
        Assert.Equal(6, BankExchangeMath.Credit(100f, 1000f, 12500f, 0.06f));
    }

    [Fact]
    public void LimitsNeedTheWholeAmountCovered()
    {
        Assert.True(BankExchangeMath.WithinLimits(5f, 5f, 5f));
        Assert.False(BankExchangeMath.WithinLimits(10f, 5f, 100f));
        Assert.False(BankExchangeMath.WithinLimits(10f, 100f, 9f));
        Assert.False(BankExchangeMath.WithinLimits(0f, 100f, 100f));
    }
}

public class AccountMathTests
{
    [Theory]
    [InlineData(false, 12.9f, 12f)]
    [InlineData(true, 12.345f, 12.35f)]
    [InlineData(true, 0.004f, 0f)]
    public void NormalizeKeepsWholeCoinsAndTwoDecimals(bool rm, float amount, float expected)
    {
        bool ok = AccountMath.TryNormalize(rm, amount, out float value);
        Assert.Equal(expected > 0f, ok);
        if (ok)
            Assert.Equal(expected, value, 4);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(200000000f)]
    public void NormalizeRefusesUnusableAmounts(float amount)
    {
        Assert.False(AccountMath.TryNormalize(true, amount, out _));
        Assert.False(AccountMath.TryNormalize(false, amount, out _));
    }

    [Fact]
    public void CoversToleratesFloatNoiseOnRealMoney()
    {
        Assert.True(AccountMath.Covers(true, 0, 1.29999f, 1.3f));
        Assert.False(AccountMath.Covers(true, 0, 1.2f, 1.3f));
        Assert.False(AccountMath.Covers(true, 0, float.NaN, 1f));
        Assert.True(AccountMath.Covers(false, 5, 0f, 5f));
        Assert.False(AccountMath.Covers(false, 4, 0f, 5f));
    }

    [Fact]
    public void DebitNeverGoesNegativeAndRoundsToHundredths()
    {
        Assert.Equal(0f, AccountMath.DebitRm(1f, 2f));
        Assert.Equal(0.67f, AccountMath.DebitRm(1f, 0.333f), 4);
        Assert.Equal(1.35f, AccountMath.CreditRm(1f, 0.345f), 4);
    }

    [Fact]
    public void CoinsFitStopsAtIntMax()
    {
        Assert.True(AccountMath.CoinsFit(int.MaxValue - 5, 5f));
        Assert.False(AccountMath.CoinsFit(int.MaxValue - 5, 6f));
    }

    [Theory]
    [InlineData(0.97f, 1.0f)]
    [InlineData(0.9f, 0.9f)]
    [InlineData(0.91f, 1.0f)]
    [InlineData(0.05f, 0.1f)]
    [InlineData(0f, 0f)]
    public void WalletTenthsRoundsADebtUp(float debt, float expected)
    {
        Assert.Equal(expected, AccountMath.WalletTenths(debt), 4);
    }
}
