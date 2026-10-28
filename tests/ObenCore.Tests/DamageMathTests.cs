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
