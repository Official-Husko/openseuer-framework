using ObenFramework.Economy;

namespace ObenCore.Tests.Economy;

public class ShopHoursMathTests
{
    [Theory]
    [InlineData(8, 18, 7.99f, false)]
    [InlineData(8, 18, 8f, true)]
    [InlineData(8, 18, 17.99f, true)]
    [InlineData(8, 18, 18f, false)]
    public void DayHoursIncludeOpeningButNotClosing(int opens, int closes, float hour, bool open) =>
        Assert.Equal(open, ShopHoursMath.IsOpenAt(opens, closes, hour));

    [Theory]
    [InlineData(20, 4, 23f, true)]
    [InlineData(20, 4, 2f, true)]
    [InlineData(20, 4, 4f, false)]
    [InlineData(20, 4, 12f, false)]
    public void NightHoursRunPastMidnight(int opens, int closes, float hour, bool open) =>
        Assert.Equal(open, ShopHoursMath.IsOpenAt(opens, closes, hour));

    [Fact]
    public void EqualHoursAreClosedAllDay()
    {
        Assert.False(ShopHoursMath.IsOpenAt(8, 8, 8f));
        Assert.Equal(0, ShopHoursMath.HoursOpen(8, 8));
    }

    [Fact]
    public void HoursOpenCountsAcrossMidnight()
    {
        Assert.Equal(10, ShopHoursMath.HoursOpen(8, 18));
        Assert.Equal(8, ShopHoursMath.HoursOpen(20, 4));
        Assert.Equal(24, ShopHoursMath.HoursOpen(0, 24));
    }

    [Fact]
    public void GameWeekdaysStartOnMonday()
    {
        Assert.Equal(0, ShopHoursMath.GameWeekday(DayOfWeek.Monday));
        Assert.Equal(5, ShopHoursMath.GameWeekday(DayOfWeek.Saturday));
        Assert.Equal(6, ShopHoursMath.GameWeekday(DayOfWeek.Sunday));
    }
}
