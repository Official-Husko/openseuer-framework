using ObenFramework.Player;

namespace ObenCore.Tests;

public class DamageMathTests
{
    [Theory]
    [InlineData(10, 1f, 0.9f, 10)]
    [InlineData(10, 2f, 0.9f, 20)]
    [InlineData(10, 0f, 0f, 0)]
    [InlineData(0, 3f, 0f, 0)]
    [InlineData(-5, 2f, 0f, 0)]
    [InlineData(3, 0.5f, 0.6f, 1)]
    [InlineData(3, 0.5f, 0.4f, 2)]
    public void ScalesAndRoundsByChance(int damage, float multiplier, float roll, int expected) =>
        Assert.Equal(expected, DamageMath.Scale(damage, multiplier, roll));

    [Fact]
    public void FractionsAverageOut()
    {
        int total = 0;
        const int Rolls = 1000;
        for (int i = 0; i < Rolls; i++)
            total += DamageMath.Scale(1, 1.5f, (i + 0.5f) / Rolls);
        Assert.Equal(1500, total);
    }

    [Fact]
    public void NaNMultiplierDealsNothing() => Assert.Equal(0, DamageMath.Scale(5, float.NaN, 0f));

    [Fact]
    public void HugeResultsAreCapped() => Assert.Equal(int.MaxValue, DamageMath.Scale(int.MaxValue, 10f, 0f));
}

public class HitRoundingTests
{
    [Theory]
    [InlineData(1, 1.5f, 2)]
    [InlineData(3, 0.5f, 2)]
    [InlineData(5, 0.3f, 2)]
    [InlineData(10, 0.04f, 0)]
    public void NearestIgnoresTheRoll(int damage, float multiplier, int expected)
    {
        Assert.Equal(expected, DamageMath.Scale(damage, multiplier, 0f, HitRounding.Nearest));
        Assert.Equal(expected, DamageMath.Scale(damage, multiplier, 0.99f, HitRounding.Nearest));
    }

    [Fact]
    public void ChanceIsTheDefault()
    {
        var roundings = new HitRoundings();
        Assert.Equal(HitRounding.Chance, roundings.For(1));
    }

    [Fact]
    public void TheNewestChoiceOnATargetDecides()
    {
        var roundings = new HitRoundings();
        roundings.Set("A", 1, null, HitRounding.Nearest);
        roundings.Set("B", 1, null, HitRounding.Chance);
        roundings.Set("C", 2, null, HitRounding.Nearest);
        Assert.Equal(HitRounding.Chance, roundings.For(1));
        Assert.Equal(HitRounding.Nearest, roundings.For(2));

        roundings.Remove("B", 1, null);
        Assert.Equal(HitRounding.Nearest, roundings.For(1));

        roundings.Set("A", 1, null, HitRounding.Chance);   // setting again moves it to the end
        roundings.Set("B", 1, "k", HitRounding.Nearest);
        roundings.RemoveOwner("B");
        Assert.Equal(HitRounding.Chance, roundings.For(1));
    }
}
