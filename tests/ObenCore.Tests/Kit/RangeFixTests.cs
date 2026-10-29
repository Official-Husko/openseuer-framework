using ObenUIKit.Menu.Settings;

namespace ObenCore.Tests.Kit;

public class RangeFixTests
{
    [Theory]
    [InlineData(2.0, 2.0)]
    [InlineData(0.25, 0.25)]
    [InlineData(20.0, 20.0)]
    [InlineData(500.0, 20.0)]
    [InlineData(-3.0, 0.25)]
    public void ClampsToTheRange(double value, double expected) =>
        Assert.Equal(expected, RangeFix.Fix(value, 0.25, 20, 2));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NotANumberTakesTheDefault(double value) => Assert.Equal(2.0, RangeFix.Fix(value, 0.25, 20, 2));

    [Fact]
    public void ADefaultOutsideTheRangeIsClampedToo() => Assert.Equal(20.0, RangeFix.Fix(double.NaN, 0.25, 20, 50));

    [Fact]
    public void NoUsableDefaultGivesTheLowest() => Assert.Equal(0.25, RangeFix.Fix(double.NaN, 0.25, 20, double.NaN));
}
