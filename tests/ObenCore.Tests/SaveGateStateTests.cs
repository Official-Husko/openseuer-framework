using ObenFramework.Saves;

namespace ObenCore.Tests;

public class SaveGateStateTests
{
    [Fact]
    public void IdleByDefault()
    {
        var state = new SaveGateState();
        Assert.False(state.IsSaving(10));
        Assert.False(state.IsLoading(10, 1f, false, true));
    }

    [Fact]
    public void SavingLastsForTheFrameItStartedIn()
    {
        var state = new SaveGateState();
        state.SavingStarted(5);
        Assert.True(state.IsSaving(5));
        // A save that threw never reports it is done; the next frame lets go anyway.
        Assert.False(state.IsSaving(6));
    }

    [Fact]
    public void SavingDoneEndsItAtOnce()
    {
        var state = new SaveGateState();
        state.SavingStarted(5);
        state.SavingDone();
        Assert.False(state.IsSaving(5));
    }

    [Fact]
    public void LoadStaysBusyUntilLoadingDone()
    {
        var state = new SaveGateState();
        state.BeginLoad(10, 1f, started: false);
        Assert.True(state.IsLoading(10, 1f, true, false));
        state.BeginLoad(12, 2f, started: true);
        // Restoring has started: an idle screen no longer means the load was abandoned.
        Assert.True(state.IsLoading(13, 2.1f, false, true));
        state.LoadDone();
        Assert.False(state.IsLoading(14, 2.2f, false, true));
    }

    [Fact]
    public void AbandonedLoadIsDroppedOnceTheGameAndScreenAreIdle()
    {
        var state = new SaveGateState();
        state.BeginLoad(10, 1f, started: false);
        // Same frame: the game has not had a chance to start anything yet.
        Assert.True(state.IsLoading(10, 1f, false, true));
        // The game's own flag is still on: the load is in its file-reading step.
        Assert.True(state.IsLoading(11, 1.1f, true, true));
        // The screen is still fading or loading a scene.
        Assert.True(state.IsLoading(11, 1.1f, false, false));
        Assert.False(state.IsLoading(11, 1.1f, false, true));
        Assert.False(state.IsLoading(12, 1.2f, true, false));
    }

    [Fact]
    public void StuckLoadIsDroppedAfterTheTimeout()
    {
        var state = new SaveGateState();
        state.BeginLoad(10, 1f, started: true);
        Assert.True(state.IsLoading(500, 1f + SaveGateState.StuckSeconds - 1f, true, false));
        Assert.False(state.IsLoading(900, 1f + SaveGateState.StuckSeconds + 1f, true, false));
        Assert.True(state.DroppedStuckLoad);
        state.ClearDroppedFlag();
        Assert.False(state.DroppedStuckLoad);
    }

    [Fact]
    public void SecondAnnouncementDoesNotRestartTheClock()
    {
        var state = new SaveGateState();
        state.BeginLoad(10, 1f, started: false);
        state.BeginLoad(11, 250f, started: false);
        // Measured from the first announcement.
        Assert.False(state.IsLoading(900, 1f + SaveGateState.StuckSeconds + 1f, true, false));
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var state = new SaveGateState();
        state.SavingStarted(5);
        state.BeginLoad(5, 1f, started: true);
        state.Reset();
        Assert.False(state.IsSaving(5));
        Assert.False(state.IsLoading(5, 1f, true, false));
    }
}
