using ObenFramework.Economy;

namespace ObenCore.Tests.Economy;

public class PlayerShopMathTests
{
    private static readonly Dictionary<int, float> NoPrices = new();

    [Fact]
    public void MarkupPriceRoundsLikeTheGame()
    {
        Assert.Equal(12f, PlayerShopMath.UnitPrice(10, 1.2f, false, NoPrices, 5));
        // Mathf.RoundToInt rounds halves to even, and so do the shops.
        Assert.Equal(2f, PlayerShopMath.UnitPrice(5, 0.5f, false, NoPrices, 5));
        Assert.Equal(4f, PlayerShopMath.UnitPrice(7, 0.5f, false, NoPrices, 5));
    }

    [Fact]
    public void RealMoneyIsATenthOfTheCoinPrice()
    {
        Assert.Equal(1.2f, PlayerShopMath.UnitPrice(10, 1.2f, true, NoPrices, 5), 4);
        Assert.Equal(0.1f, PlayerShopMath.UnitPrice(1, 1f, true, NoPrices, 5), 4);
    }

    [Fact]
    public void WorthlessItemsAreNotForSale()
    {
        Assert.Equal(0f, PlayerShopMath.UnitPrice(0, 1.2f, false, NoPrices, 5));
        Assert.Equal(0f, PlayerShopMath.UnitPrice(1, 0.2f, false, NoPrices, 5));
    }

    [Fact]
    public void FixedPricesWinAndAreSettledToTheCurrency()
    {
        var prices = new Dictionary<int, float> { [5] = 7.6f, [6] = 0f };
        Assert.Equal(8f, PlayerShopMath.UnitPrice(100, 1.2f, false, prices, 5));
        Assert.Equal(7.6f, PlayerShopMath.UnitPrice(100, 1.2f, true, prices, 5), 4);
        Assert.Equal(0f, PlayerShopMath.UnitPrice(100, 1.2f, false, prices, 6));
        Assert.Equal(120f, PlayerShopMath.UnitPrice(100, 1.2f, false, prices, 7));
    }

    [Theory]
    [InlineData(1.2f, true)]
    [InlineData(10f, true)]
    [InlineData(0f, false)]
    [InlineData(-1f, false)]
    [InlineData(10.5f, false)]
    [InlineData(float.NaN, false)]
    public void MarkupLimits(float markup, bool ok) => Assert.Equal(ok, PlayerShopMath.ValidMarkup(markup));

    [Fact]
    public void TotalsKeepRealMoneyInTenths()
    {
        Assert.Equal(36f, PlayerShopMath.Total(12f, 3, false));
        Assert.Equal(0.3f, PlayerShopMath.Total(0.1f, 3, true), 4);
    }

    [Fact]
    public void SellNeedsTheRightItemEnoughOfItAndAPrice()
    {
        Assert.True(PlayerShopMath.CanSell(5, 5, 3, 3, 1f));
        Assert.False(PlayerShopMath.CanSell(5, 6, 3, 1, 1f));
        Assert.False(PlayerShopMath.CanSell(5, 5, 2, 3, 1f));
        Assert.False(PlayerShopMath.CanSell(5, 5, 3, 0, 1f));
        Assert.False(PlayerShopMath.CanSell(5, 5, 3, 1, 0f));
    }

    [Fact]
    public void TillCreditAndDebit()
    {
        int coins = 0;
        float rm = 0f;
        Assert.True(PlayerShopMath.Credit(ref coins, ref rm, false, 12.9f));
        Assert.True(PlayerShopMath.Credit(ref coins, ref rm, true, 0.3f));
        Assert.Equal(12, coins);
        Assert.Equal(0.3f, rm, 4);

        Assert.False(PlayerShopMath.Debit(ref coins, ref rm, false, 13f));
        Assert.Equal(12, coins);
        Assert.True(PlayerShopMath.Debit(ref coins, ref rm, false, 12f));
        Assert.True(PlayerShopMath.Debit(ref coins, ref rm, true, 0.3f));
        Assert.Equal(0, coins);
        Assert.Equal(0f, rm);
        Assert.False(PlayerShopMath.Credit(ref coins, ref rm, false, -1f));
    }

    [Fact]
    public void TillIsCapped()
    {
        int coins = (int)PlayerShopMath.MaxTill - 1;
        float rm = 0f;
        PlayerShopMath.Credit(ref coins, ref rm, false, 50f);
        Assert.Equal((int)PlayerShopMath.MaxTill, coins);
    }
}
