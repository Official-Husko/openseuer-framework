using GameTweaks;

namespace ObenCore.Tests.Tweaks;

public class TweakMathTests
{
    [Theory]
    [InlineData(2f, 0f, 10f, 2f)]
    [InlineData(-1f, 0f, 10f, 0f)]
    [InlineData(50f, 0f, 10f, 10f)]
    [InlineData(float.NaN, 0f, 10f, 1f)]
    [InlineData(float.PositiveInfinity, 0f, 10f, 1f)]
    [InlineData(float.NegativeInfinity, 0.5f, 10f, 1f)]
    public void FactorClampsAndRejectsNonNumbers(float value, float min, float max, float expected) =>
        Assert.Equal(expected, TweakMath.Factor(value, min, max));

    [Fact]
    public void FactorUsesTheGivenFallback() => Assert.Equal(2.5f, TweakMath.Factor(float.NaN, 0.5f, 4f, 2.5f));

    [Theory]
    [InlineData(99, 2f, 198)]
    [InlineData(20, 0.5f, 10)]
    [InlineData(5, 0.25f, 1)]
    [InlineData(3, 0.05f, 1)]
    [InlineData(10, 1.25f, 13)]
    public void StacksScaleAndNeverDropBelowOne(int stack, float factor, int expected) =>
        Assert.Equal(expected, TweakMath.Stack(stack, factor));

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ItemsThatDontStackStaySingle(int stack) => Assert.Equal(stack, TweakMath.Stack(stack, 10f));

    [Fact]
    public void HugeStacksStopAtIntMax() => Assert.Equal(int.MaxValue, TweakMath.Stack(int.MaxValue / 2, 100f));

    [Theory]
    [InlineData(1000, 0.5f, 500)]
    [InlineData(333, 0.5f, 167)]
    [InlineData(250, 0f, 0)]
    [InlineData(250, 2f, 500)]
    [InlineData(-10, 2f, 0)]
    public void PricesRoundAndStayPositive(int price, float factor, int expected) =>
        Assert.Equal(expected, TweakMath.Price(price, factor));

    [Theory]
    [InlineData(48, 0.5f, 24)]
    [InlineData(5, 0.5f, 3)]
    [InlineData(1, 0.01f, 1)]
    [InlineData(0, 2f, 0)]
    [InlineData(12, 2f, 24)]
    public void HoursRoundUp(int hours, float factor, int expected) =>
        Assert.Equal(expected, TweakMath.Hours(hours, factor));

    [Fact]
    public void ScriptedOrdersKeepTheirHours()
    {
        Assert.Equal(30f, TweakMath.OrderHours(30f, 0.5f, instant: false, scripted: true));
        Assert.Equal(30f, TweakMath.OrderHours(30f, 0.5f, instant: true, scripted: true));
    }

    [Fact]
    public void InstantOrdersStartAtZero() => Assert.Equal(0f, TweakMath.OrderHours(30f, 2f, instant: true, scripted: false));

    [Fact]
    public void OrdersScale() => Assert.Equal(15f, TweakMath.OrderHours(30f, 0.5f, instant: false, scripted: false));

    [Theory]
    [InlineData(96f, 2f, 48f)]
    [InlineData(96f, 0.5f, 192f)]
    [InlineData(96f, 1f, 96f)]
    [InlineData(96f, 0f, 96f)]
    [InlineData(96f, -3f, 96f)]
    [InlineData(96f, float.NaN, 96f)]
    public void FasterDividesByAPositiveSpeedOnly(float duration, float speed, float expected) =>
        Assert.Equal(expected, TweakMath.Faster(duration, speed));

    [Theory]
    [InlineData(60f, 0.5f, 30f)]
    [InlineData(60f, 0f, 0f)]
    [InlineData(60f, 1f, 60f)]
    public void SpoilingScales(float minutes, float factor, float expected) =>
        Assert.Equal(expected, TweakMath.Spoil(minutes, factor));
}
