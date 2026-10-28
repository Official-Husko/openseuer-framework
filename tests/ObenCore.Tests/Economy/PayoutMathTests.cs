using ObenFramework.Gambling;
using ObenFramework.Player;

namespace ObenCore.Tests.Economy;

public class PayoutMathTests
{
    [Fact]
    public void ScaleRoundsHalfToEvenLikeTheGame()
    {
        Assert.Equal(10, PayoutMath.Scale(5, 2f));
        Assert.Equal(2, PayoutMath.Scale(5, 0.5f));
        Assert.Equal(4, PayoutMath.Scale(7, 0.5f));
    }

    [Fact]
    public void ScaleLeavesNothingAndOneAlone()
    {
        Assert.Equal(0, PayoutMath.Scale(0, 3f));
        Assert.Equal(-4, PayoutMath.Scale(-4, 3f));
        Assert.Equal(17, PayoutMath.Scale(17, 1f));
    }

    [Fact]
    public void ZeroOrBrokenFactorsPayNothing()
    {
        Assert.Equal(0, PayoutMath.Scale(17, 0f));
        Assert.Equal(0, PayoutMath.Scale(17, float.NaN));
        Assert.Equal(0, PayoutMath.Scale(17, -2f));
    }

    [Fact]
    public void ScaleIsCapped()
    {
        Assert.Equal(PayoutMath.MaxPayout, PayoutMath.Scale(int.MaxValue / 2, 10f));
    }

    [Fact]
    public void RealMoneyKeepsTenths()
    {
        Assert.Equal(3.8f, PayoutMath.ScaleTenths(2.5f, 1.5f), 4);
        Assert.Equal(0f, PayoutMath.ScaleTenths(2.5f, 0f));
        Assert.Equal(2.5f, PayoutMath.ScaleTenths(2.5f, 1f));
    }

    [Theory]
    [InlineData(10, 1, 20)]
    [InlineData(10, 2, 12)]
    [InlineData(10, 3, 8)]
    [InlineData(10, 4, 0)]
    [InlineData(25, 2, 30)]
    [InlineData(7, 3, 6)]
    [InlineData(10, 0, -1)]
    [InlineData(10, 5, -1)]
    public void CardTablePaysByPlace(int stake, int place, int expected) =>
        Assert.Equal(expected, PayoutMath.CardPayout(stake, place));

    [Fact]
    public void CombineCaps()
    {
        Assert.Equal(6f, PayoutMath.Combine(2f, 3f, 10f));
        Assert.Equal(10f, PayoutMath.Combine(4f, 4f, 10f));
    }

    [Fact]
    public void GamesShareTheMultiplierTable()
    {
        // Payouts keeps one channel per GambleKind, All being channel 0, on the shared MultiplierTable.
        var table = new MultiplierTable(Enum.GetValues(typeof(GambleKind)).Length, 10f);
        table.Set("a", (int)GambleKind.All, null, 2f);
        table.Set("b", (int)GambleKind.SlotMachine, null, 1.5f);
        float slot = PayoutMath.Combine(table.Product((int)GambleKind.All), table.Product((int)GambleKind.SlotMachine), 10f);
        float pajatso = PayoutMath.Combine(table.Product((int)GambleKind.All), table.Product((int)GambleKind.Pajatso), 10f);
        Assert.Equal(3f, slot);
        Assert.Equal(2f, pajatso);
    }
}
