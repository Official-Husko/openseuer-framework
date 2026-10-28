using ObenFramework.Events;
using ObenFramework.NewGames;

namespace ObenCore.Tests;

public class LoadAbortRulesTests
{
    [Fact]
    public void NothingAnnouncedNothingDropped()
    {
        var rules = new LoadAbortRules();
        Assert.False(rules.Check(10, gameLoading: false, screenIdle: true));
    }

    [Fact]
    public void NotInTheFrameOfTheAnnouncement()
    {
        var rules = new LoadAbortRules();
        rules.Announced(5, levelChange: false, worldReady: true);
        Assert.False(rules.Check(5, false, true));
        Assert.True(rules.Check(6, false, true));
        Assert.False(rules.Pending);
    }

    [Fact]
    public void WaitsWhileTheGameLoadsOrTheScreenFades()
    {
        var rules = new LoadAbortRules();
        rules.Announced(1, levelChange: true, worldReady: true);
        Assert.False(rules.Check(2, gameLoading: true, screenIdle: true));
        Assert.False(rules.Check(3, gameLoading: false, screenIdle: false));
        Assert.True(rules.Check(4, false, true));
        Assert.True(rules.LevelChange);
        Assert.True(rules.WasReady);
    }

    [Fact]
    public void ASceneLoadMeansTheLoadHappened()
    {
        var rules = new LoadAbortRules();
        rules.Announced(1, levelChange: true, worldReady: true);
        rules.Proceeding();
        Assert.False(rules.Check(50, false, true));
    }

    [Fact]
    public void LevelChangeThenLoadIsOneAnnouncement()
    {
        var rules = new LoadAbortRules();
        rules.Announced(1, levelChange: true, worldReady: true);
        rules.Announced(8, levelChange: false, worldReady: false);
        Assert.False(rules.Check(8, false, true));
        Assert.True(rules.Check(9, false, true));
        Assert.True(rules.LevelChange);
        Assert.True(rules.WasReady);
    }

    [Fact]
    public void ReportsOnlyOnce()
    {
        var rules = new LoadAbortRules();
        rules.Announced(1, false, false);
        Assert.True(rules.Check(2, false, true));
        Assert.False(rules.Check(3, false, true));
    }
}

public class SessionAbortTests
{
    [Fact]
    public void AbortedPauseMenuLoadKeepsTheSession()
    {
        var rules = new SessionRules();
        rules.NewGameStarted();
        rules.WorldReady(false);
        rules.WillLoad();
        rules.LoadAborted();
        rules.LevelChanging();
        rules.WillLoad();
        Assert.Equal(SessionKind.None, rules.WorldReady(false));
        Assert.Equal(SessionKind.NewGame, rules.Current);
    }

    [Fact]
    public void SameLevelDoorDoesNotSwallowTheNextLoad()
    {
        var rules = new SessionRules();
        rules.WillLoad();
        rules.WorldReady(false);
        rules.LevelChanging();
        rules.LoadAborted();
        rules.WillLoad();
        Assert.Equal(SessionKind.SaveLoad, rules.WorldReady(false));
        Assert.False(rules.LevelChangePending);
    }

    [Fact]
    public void AbortedLoadFromTheMenuStartsNothing()
    {
        var rules = new SessionRules();
        rules.MainMenu();
        rules.WillLoad();
        rules.LoadAborted();
        Assert.Equal(SessionKind.Direct, rules.WorldReady(false));
    }
}
