using ObenLoader.Logging;

namespace ObenCore.Tests;

public class LineLimiterTests
{
    [Fact]
    public void LetsLinesThroughUpToTheCap()
    {
        var limiter = new LineLimiter(3);
        for (int i = 0; i < 3; i++)
            Assert.True(limiter.TryTake(100 + i, out _));
        Assert.False(limiter.TryTake(200, out _));
        Assert.Equal(1, limiter.PendingDrops);
        Assert.Equal(1, limiter.TotalDropped);
    }

    [Fact]
    public void NextSecondReportsWhatWasDropped()
    {
        var limiter = new LineLimiter(2);
        limiter.TryTake(0, out _);
        limiter.TryTake(1, out _);
        limiter.TryTake(2, out _);
        limiter.TryTake(3, out _);

        Assert.True(limiter.TryTake(1000, out int dropped));
        Assert.Equal(2, dropped);
        Assert.Equal(0, limiter.PendingDrops);
        Assert.True(limiter.TryTake(1001, out dropped));
        Assert.Equal(0, dropped);
    }

    [Fact]
    public void FinishedDropsAreOnlyTakenAfterTheWindow()
    {
        var limiter = new LineLimiter(1);
        limiter.TryTake(0, out _);
        limiter.TryTake(10, out _);

        Assert.Equal(0, limiter.TakeFinishedDrops(500));
        Assert.Equal(1, limiter.TakeFinishedDrops(1500));
        Assert.Equal(0, limiter.TakeFinishedDrops(1600));
    }

    [Fact]
    public void ClockGoingBackwardsStartsANewWindow()
    {
        var limiter = new LineLimiter(1);
        Assert.True(limiter.TryTake(5000, out _));
        Assert.True(limiter.TryTake(10, out _));
    }

    [Theory]
    [InlineData(0, LineLimiter.MinPerSecond)]
    [InlineData(-5, LineLimiter.MinPerSecond)]
    [InlineData(50, 50)]
    [InlineData(int.MaxValue, LineLimiter.MaxPerSecond)]
    public void CapIsClamped(int asked, int expected)
    {
        Assert.Equal(expected, new LineLimiter(asked).PerSecond);
    }
}

public class OnceKeysTests
{
    [Fact]
    public void OnlyTheFirstUseCounts()
    {
        var keys = new OnceKeys();
        Assert.True(keys.First("a"));
        Assert.False(keys.First("a"));
        Assert.True(keys.First("b"));
    }

    [Fact]
    public void ForgetByPrefixKeepsTheRest()
    {
        var keys = new OnceKeys();
        keys.First("items:x");
        keys.First("npcs:x");
        keys.Forget("items:");
        Assert.True(keys.First("items:x"));
        Assert.False(keys.First("npcs:x"));
        keys.Forget();
        Assert.True(keys.First("npcs:x"));
    }

    [Fact]
    public void StartsOverWhenFull()
    {
        var keys = new OnceKeys(2);
        keys.First("a");
        keys.First("b");
        Assert.True(keys.First("c"));
        Assert.Equal(1, keys.Count);
        Assert.True(keys.First("a"));
    }

    [Fact]
    public void NullKeyIsAKeyToo()
    {
        var keys = new OnceKeys();
        Assert.True(keys.First(null!));
        Assert.False(keys.First(null!));
    }
}

public class ChangeFilterTests
{
    [Fact]
    public void OnlyChangesPass()
    {
        var filter = new ChangeFilter();
        Assert.True(filter.Changed("door", "open"));
        Assert.False(filter.Changed("door", "open"));
        Assert.True(filter.Changed("door", "closed"));
        Assert.True(filter.Changed("door", "open"));
    }

    [Fact]
    public void KeysAreIndependent()
    {
        var filter = new ChangeFilter();
        Assert.True(filter.Changed("a", "1"));
        Assert.True(filter.Changed("b", "1"));
        Assert.False(filter.Changed("a", "1"));
    }

    [Fact]
    public void ForgottenKeyLogsAgain()
    {
        var filter = new ChangeFilter();
        filter.Changed("a", "1");
        filter.Forget("a");
        Assert.True(filter.Changed("a", "1"));
    }

    [Fact]
    public void NullValuesCompare()
    {
        var filter = new ChangeFilter();
        Assert.True(filter.Changed("a", null!));
        Assert.False(filter.Changed("a", null!));
        Assert.True(filter.Changed("a", "x"));
    }
}
