using ObenFramework.Housing;

namespace ObenCore.Tests;

public class HousingRulesTests
{
    [Theory]
    [InlineData(18f, 18f)]
    [InlineData(0f, HousingRules.MinTimerHours)]
    [InlineData(-5f, HousingRules.MinTimerHours)]
    [InlineData(float.NaN, HousingRules.MinTimerHours)]
    [InlineData(1e9f, HousingRules.MaxTimerHours)]
    [InlineData(float.PositiveInfinity, HousingRules.MaxTimerHours)]
    public void TimersStayWithinWhatTheGameCanCount(float hours, float expected) =>
        Assert.Equal(expected, HousingRules.ClampTimerHours(hours));

    [Fact]
    public void TheLongestTimerFitsTheGamesSecondCounter()
    {
        float seconds = HousingRules.MaxTimerHours * 3600f;
        Assert.True(seconds < int.MaxValue);
        Assert.True((int)(HousingRules.MinTimerHours * 3600f) >= 0);
    }

    [Theory]
    [InlineData(-1, CleaningState.NotStarted)]
    [InlineData(0, CleaningState.Done)]
    [InlineData(42, CleaningState.Running)]
    public void CleaningDayMeansWhatTheGameMeans(int day, CleaningState expected) =>
        Assert.Equal(expected, HousingRules.CleaningOf(day));

    [Theory]
    [InlineData(0, ApartmentSize.Small)]
    [InlineData(2, ApartmentSize.Large)]
    [InlineData(3, ApartmentSize.Shop)]
    [InlineData(4, ApartmentSize.Unknown)]
    [InlineData(-1, ApartmentSize.Unknown)]
    public void SizesFollowTheGamesNumbers(int game, ApartmentSize expected) =>
        Assert.Equal(expected, HousingRules.SizeOf(game));

    [Fact]
    public void RenovationIdsRoundTrip()
    {
        string id = HousingRules.RenovationId(7, "kitchen/2");
        Assert.Equal("7/kitchen/2", id);
        Assert.True(HousingRules.TryParseRenovationId(id, out int apartment, out string upgrade));
        Assert.Equal(7, apartment);
        Assert.Equal("kitchen/2", upgrade);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kitchen")]
    [InlineData("/kitchen")]
    [InlineData("3/")]
    [InlineData("-1/roof")]
    [InlineData("x/roof")]
    public void BadRenovationIdsAreRejected(string id) =>
        Assert.False(HousingRules.TryParseRenovationId(id, out _, out _));

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void HotelStaysAreOneToSevenNights(int nights) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HousingRules.RequireNights(nights));

    [Fact]
    public void HoursFromSeconds() => Assert.Equal(1.5f, HousingRules.HoursOf(5400));
}
