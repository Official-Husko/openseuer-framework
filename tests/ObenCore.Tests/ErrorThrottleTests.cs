using ObenLoader.Logging;

namespace ObenCore.Tests;

public class ErrorThrottleTests
{
    [Fact]
    public void TheSameErrorOnlyOnceInTenSeconds()
    {
        var throttle = new ErrorThrottle();
        Assert.True(throttle.TryWrite("boom", 0, out _));
        Assert.False(throttle.TryWrite("boom", 5_000, out _));
        Assert.True(throttle.TryWrite("other", 5_000, out _));
        Assert.True(throttle.TryWrite("boom", 10_000, out int heldBefore));
        Assert.Equal(1, heldBefore);
        Assert.Equal(1, throttle.TotalHeldBack);
    }

    [Fact]
    public void AtMostTwentyErrorsInTenSeconds()
    {
        var throttle = new ErrorThrottle();
        for (int i = 0; i < ErrorThrottle.MaxPerWindow; i++)
            Assert.True(throttle.TryWrite("error " + i, i, out _));
        Assert.False(throttle.TryWrite("one more", 100, out _));
        Assert.False(throttle.TryWrite("and another", 200, out _));
        Assert.Equal(2, throttle.PendingHeldBack);

        Assert.True(throttle.TryWrite("next window", 10_000, out int heldBefore));
        Assert.Equal(2, heldBefore);
        Assert.Equal(0, throttle.PendingHeldBack);
    }

    [Fact]
    public void HeldBackCountIsOnlyTakenOnceTheWindowIsOver()
    {
        var throttle = new ErrorThrottle();
        throttle.TryWrite("boom", 0, out _);
        throttle.TryWrite("boom", 1_000, out _);

        Assert.Equal(0, throttle.TakeFinishedHeldBack(9_999));
        Assert.Equal(1, throttle.TakeFinishedHeldBack(10_000));
        Assert.Equal(0, throttle.TakeFinishedHeldBack(20_000));
    }

    [Fact]
    public void AClockGoingBackStartsAFreshWindow()
    {
        var throttle = new ErrorThrottle();
        throttle.TryWrite("boom", 50_000, out _);
        Assert.True(throttle.TryWrite("boom", 1_000, out _));
    }

    [Fact]
    public void ManyDifferentKeysDoNotGrowWithoutLimit()
    {
        var throttle = new ErrorThrottle();
        for (int i = 0; i < 2_000; i++)
            throttle.TryWrite("key " + i, i * 1_000L, out _);
        // One new key a second is 10 a window, under the budget; the key set starts over when full.
        Assert.Equal(0, throttle.PendingHeldBack);
        Assert.Equal(0, throttle.TotalHeldBack);
    }
}
