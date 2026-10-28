using ObenFramework.Clock;

namespace ObenCore.Tests;

public class TimeOverridesTests
{
    private readonly object _a = new object();
    private readonly object _b = new object();

    [Fact]
    public void NothingHeldLeavesTheDayLengthAlone()
    {
        var overrides = new TimeOverrides();
        Assert.False(overrides.Any);
        Assert.Equal(96f, overrides.DayLength(96f, 1f));
        Assert.Equal(1f, overrides.Speed);
    }

    [Fact]
    public void FreezeStopsTheClockUntilEveryoneLetsGo()
    {
        var overrides = new TimeOverrides();
        overrides.Freeze(_a);
        overrides.Freeze(_b);
        Assert.Equal(0f, overrides.DayLength(96f, 1f));
        overrides.Unfreeze(_a);
        Assert.Equal(0f, overrides.DayLength(96f, 1f));
        overrides.Unfreeze(_b);
        Assert.Equal(96f, overrides.DayLength(96f, 1f));
    }

    [Fact]
    public void FreezingTwiceIsOneFreeze()
    {
        var overrides = new TimeOverrides();
        Assert.True(overrides.Freeze(_a));
        Assert.False(overrides.Freeze(_a));
        overrides.Unfreeze(_a);
        Assert.False(overrides.IsFrozen);
    }

    [Fact]
    public void NewestSpeedWinsAndTheOneBeforeComesBack()
    {
        var overrides = new TimeOverrides();
        overrides.SetSpeed(_a, 2f);
        overrides.SetSpeed(_b, 4f);
        Assert.Equal(24f, overrides.DayLength(96f, 1f));
        overrides.ClearSpeed(_b);
        Assert.Equal(48f, overrides.DayLength(96f, 1f));
    }

    [Fact]
    public void SettingASpeedAgainMakesItTheNewest()
    {
        var overrides = new TimeOverrides();
        overrides.SetSpeed(_a, 2f);
        overrides.SetSpeed(_b, 4f);
        overrides.SetSpeed(_a, 0.5f);
        Assert.Equal(0.5f, overrides.Speed);
        Assert.Equal(2, overrides.Speeds.Count);
    }

    [Fact]
    public void FreezeBeatsSpeed()
    {
        var overrides = new TimeOverrides();
        overrides.SetSpeed(_a, 3f);
        overrides.Freeze(_b);
        Assert.Equal(0f, overrides.DayLength(96f, 1f));
    }

    [Fact]
    public void GameSpeedUpsRunAsTheGameWants()
    {
        var overrides = new TimeOverrides();
        overrides.Freeze(_a);
        Assert.Equal(0.64f, overrides.DayLength(0.64f, 150f));
        // The game slowing time down (card games) keeps the freeze.
        Assert.Equal(0f, overrides.DayLength(384f, 0.25f));
    }

    [Fact]
    public void ReleaseDropsEverythingAnOwnerHolds()
    {
        var overrides = new TimeOverrides();
        overrides.Freeze(_a);
        overrides.SetSpeed(_a, 2f);
        overrides.SetSpeed(_b, 3f);
        Assert.True(overrides.Release(_a));
        Assert.False(overrides.IsFrozen);
        Assert.Equal(3f, overrides.Speed);
        Assert.False(overrides.Release(_a));
    }

    [Theory]
    [InlineData(0.001f, TimeOverrides.MinSpeed)]
    [InlineData(1000f, TimeOverrides.MaxSpeed)]
    [InlineData(5f, 5f)]
    public void SpeedIsClamped(float asked, float expected) =>
        Assert.Equal(expected, new TimeOverrides().SetSpeed(_a, asked));

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void ZeroOrLessIsRefused(float speed) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeOverrides().SetSpeed(_a, speed));
}
