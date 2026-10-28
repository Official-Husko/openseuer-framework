using ObenFramework.Env;

namespace ObenCore.Tests.Env;

public class FadeTrackTests
{
    [Fact]
    public void ZeroSecondsJumpsAtOnce()
    {
        var track = new FadeTrack(2);
        track.FadeTo(new[] { 1f, 0.5f }, 0f);
        Assert.False(track.Running);
        Assert.Equal(new[] { 1f, 0.5f }, track.Current);
    }

    [Fact]
    public void FadesLinearlyAndStopsAtTheTarget()
    {
        var track = new FadeTrack(1);
        track.FadeTo(new[] { 1f }, 2f);
        Assert.True(track.Running);
        Assert.True(track.Step(1f));
        Assert.Equal(0.5f, track.Current[0], 3);
        Assert.False(track.Step(5f));
        Assert.Equal(1f, track.Current[0]);
        Assert.False(track.Step(1f));
    }

    [Fact]
    public void ANewFadeStartsFromWhatIsShown()
    {
        var track = new FadeTrack(1);
        track.FadeTo(new[] { 1f }, 1f);
        track.Step(0.5f);
        track.FadeTo(new[] { 0f }, 1f);
        Assert.Equal(0.5f, track.Current[0], 3);
        track.Step(0.5f);
        Assert.Equal(0.25f, track.Current[0], 3);
    }

    [Fact]
    public void RejectsATargetOfTheWrongSize()
    {
        var track = new FadeTrack(3);
        Assert.Throws<ArgumentException>(() => track.FadeTo(new[] { 1f }, 0f));
    }

    [Fact]
    public void InfiniteSecondsJump()
    {
        var track = new FadeTrack(1);
        track.FadeTo(new[] { 4f }, float.PositiveInfinity);
        Assert.Equal(4f, track.Current[0]);
    }
}
