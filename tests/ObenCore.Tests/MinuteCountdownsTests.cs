using ObenFramework.Clock;

namespace ObenCore.Tests;

public class MinuteCountdownsTests
{
    private readonly object _owner = new object();

    [Fact]
    public void RunsAfterTheMinutesPassed()
    {
        var countdowns = new MinuteCountdowns();
        int ran = 0;
        countdowns.Add(_owner, 3, () => ran++);
        countdowns.Tick(null);
        countdowns.Tick(null);
        Assert.Equal(0, ran);
        countdowns.Tick(null);
        Assert.Equal(1, ran);
        Assert.Equal(0, countdowns.Count);
        countdowns.Tick(null);
        Assert.Equal(1, ran);
    }

    [Fact]
    public void DueActionsRunOldestFirst()
    {
        var countdowns = new MinuteCountdowns();
        var order = new List<int>();
        countdowns.Add(_owner, 1, () => order.Add(1));
        countdowns.Add(_owner, 1, () => order.Add(2));
        countdowns.Add(_owner, 1, () => order.Add(3));
        countdowns.Tick(null);
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void CancelledActionsDontRun()
    {
        var countdowns = new MinuteCountdowns();
        bool ran = false;
        var entry = countdowns.Add(_owner, 1, () => ran = true);
        Assert.True(countdowns.Cancel(entry));
        Assert.False(countdowns.Cancel(entry));
        countdowns.Tick(null);
        Assert.False(ran);
    }

    [Fact]
    public void CancelAllOnlyTouchesThatOwner()
    {
        var countdowns = new MinuteCountdowns();
        var other = new object();
        int ran = 0;
        countdowns.Add(_owner, 1, () => ran += 1);
        countdowns.Add(other, 1, () => ran += 10);
        Assert.Equal(1, countdowns.CancelAll(_owner));
        countdowns.Tick(null);
        Assert.Equal(10, ran);
    }

    [Fact]
    public void AThrowingActionDoesNotStopTheRest()
    {
        var countdowns = new MinuteCountdowns();
        var errors = new List<Exception>();
        bool second = false;
        countdowns.Add(_owner, 1, () => throw new InvalidOperationException());
        countdowns.Add(_owner, 1, () => second = true);
        countdowns.Tick(errors.Add);
        Assert.True(second);
        Assert.Single(errors);
    }

    [Fact]
    public void ActionsScheduledWhileRunningWaitForTheirOwnMinutes()
    {
        var countdowns = new MinuteCountdowns();
        int ran = 0;
        countdowns.Add(_owner, 1, () => countdowns.Add(_owner, 1, () => ran++));
        countdowns.Tick(null);
        Assert.Equal(0, ran);
        countdowns.Tick(null);
        Assert.Equal(1, ran);
    }

    [Fact]
    public void ClearDropsEverything()
    {
        var countdowns = new MinuteCountdowns();
        var entry = countdowns.Add(_owner, 1, () => { });
        countdowns.Clear();
        Assert.Equal(0, countdowns.Count);
        Assert.False(countdowns.Cancel(entry));
    }

    [Fact]
    public void ZeroMinutesIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MinuteCountdowns().Add(_owner, 0, () => { }));
}
