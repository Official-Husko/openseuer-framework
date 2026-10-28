using ObenFramework.NewGames;
using ObenFramework.Story;

namespace ObenCore.Tests.Story;

public class RetryGateTests
{
    [Fact]
    public void PassesOnceWhenReady()
    {
        var gate = new RetryGate();
        gate.Arm();
        Assert.False(gate.Try(false));
        Assert.True(gate.Try(true));
        Assert.True(gate.Done);
        Assert.False(gate.Waiting);
        Assert.False(gate.Try(true));
    }

    [Fact]
    public void ArmingAgainAfterPassingDoesNothingUntilReset()
    {
        var gate = new RetryGate();
        gate.Arm();
        gate.Try(true);
        gate.Arm();
        Assert.False(gate.Waiting);
        gate.Reset();
        gate.Arm();
        Assert.True(gate.Waiting);
    }

    [Fact]
    public void GivesUpAfterTheLimit()
    {
        var gate = new RetryGate();
        gate.Arm();
        for (int i = 0; i < RetryGate.MaxAttempts; i++)
            Assert.False(gate.Try(false));
        Assert.False(gate.Waiting);
        Assert.True(gate.GaveUp);
        Assert.False(gate.Done);
        Assert.False(gate.Try(true));
    }

    [Fact]
    public void UnarmedNeverPasses()
    {
        var gate = new RetryGate();
        Assert.False(gate.Try(true));
        Assert.False(gate.Done);
    }
}

public class StoryQueueTests
{
    [Fact]
    public void RunsInOrderAndEmpties()
    {
        var queue = new StoryQueue();
        var seen = new List<int>();
        queue.Add(null, "a", () => seen.Add(1));
        queue.Add(null, "b", () => seen.Add(2));
        Assert.Equal(2, queue.Run(null));
        Assert.Equal(new[] { 1, 2 }, seen);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void EntriesAddedWhileRunningWaitForTheNextRun()
    {
        var queue = new StoryQueue();
        int runs = 0;
        queue.Add(null, "again", () => { runs++; queue.Add(null, "later", () => runs++); });
        queue.Run(null);
        Assert.Equal(1, runs);
        Assert.Equal(1, queue.Count);
        queue.Run(null);
        Assert.Equal(2, runs);
    }

    [Fact]
    public void AThrowingEntryDoesNotStopTheRest()
    {
        var queue = new StoryQueue();
        var failed = new List<string>();
        bool ran = false;
        queue.Add(null, "bad", () => throw new InvalidOperationException());
        queue.Add(null, "good", () => ran = true);
        queue.Run((name, _) => failed.Add(name));
        Assert.True(ran);
        Assert.Equal(new[] { "bad" }, failed);
    }

    [Fact]
    public void RemovesOnlyTheOwnersEntries()
    {
        var queue = new StoryQueue();
        object a = new object(), b = new object();
        queue.Add(a, "a1", () => { });
        queue.Add(b, "b1", () => { });
        queue.Add(a, "a2", () => { });
        Assert.Equal(2, queue.RemoveOwner(a));
        Assert.Equal(new[] { "b1" }, queue.Names());
    }

    [Fact]
    public void DropsTheOldestWhenFull()
    {
        var queue = new StoryQueue();
        for (int i = 0; i < StoryQueue.MaxEntries + 3; i++)
            queue.Add(null, i.ToString(), () => { });
        Assert.Equal(StoryQueue.MaxEntries, queue.Count);
        Assert.Equal(3, queue.Dropped);
        Assert.Equal("3", queue.Names()[0]);
    }
}

public class SessionRulesTests
{
    [Fact]
    public void SaveLoadFromTheMenuStartsASession()
    {
        var rules = new SessionRules();
        rules.MainMenu();
        rules.WillLoad();
        Assert.Equal(SessionKind.SaveLoad, rules.WorldReady(false));
        Assert.Equal(SessionKind.SaveLoad, rules.Current);
    }

    [Fact]
    public void LevelChangeContinuesTheSession()
    {
        var rules = new SessionRules();
        rules.WillLoad();
        rules.WorldReady(false);
        rules.LevelChanging();
        rules.WillLoad();
        Assert.Equal(SessionKind.None, rules.WorldReady(false));
        Assert.Equal(SessionKind.SaveLoad, rules.Current);
    }

    [Fact]
    public void NewGameWaitsPastTheIntro()
    {
        var rules = new SessionRules();
        rules.MainMenu();
        rules.NewGameStarted();
        Assert.Equal(SessionKind.None, rules.WorldReady(true));
        Assert.Equal(SessionKind.NewGame, rules.WorldReady(false));
        Assert.Equal(SessionKind.NewGame, rules.Current);
    }

    [Fact]
    public void LevelChangeAfterANewGameKeepsItANewGame()
    {
        var rules = new SessionRules();
        rules.NewGameStarted();
        rules.WorldReady(false);
        rules.LevelChanging();
        rules.WillLoad();
        Assert.Equal(SessionKind.None, rules.WorldReady(false));
        Assert.Equal(SessionKind.NewGame, rules.Current);
    }

    [Fact]
    public void LoadingASaveFromThePauseMenuStartsANewSession()
    {
        var rules = new SessionRules();
        rules.NewGameStarted();
        rules.WorldReady(false);
        rules.WillLoad();
        Assert.Equal(SessionKind.SaveLoad, rules.WorldReady(false));
    }

    [Fact]
    public void SameSaveLoadedAgainStartsAgain()
    {
        var rules = new SessionRules();
        rules.WillLoad();
        rules.WorldReady(false);
        rules.WillLoad();
        Assert.Equal(SessionKind.SaveLoad, rules.WorldReady(false));
    }

    [Fact]
    public void LevelFromTheMenuWithoutASaveIsDirect()
    {
        var rules = new SessionRules();
        rules.MainMenu();
        Assert.Equal(SessionKind.Direct, rules.WorldReady(false));
    }

    [Fact]
    public void MainMenuEndsTheSession()
    {
        var rules = new SessionRules();
        rules.WillLoad();
        rules.WorldReady(false);
        rules.LevelChanging();
        rules.MainMenu();
        Assert.Equal(SessionKind.None, rules.Current);
        rules.WillLoad();
        Assert.Equal(SessionKind.SaveLoad, rules.WorldReady(false));
    }

    [Fact]
    public void ResumeKeepsAKnownSessionOrMarksItUnknown()
    {
        var rules = new SessionRules();
        rules.Resume(SessionKind.NewGame, worldRunning: true);
        Assert.Equal(SessionKind.NewGame, rules.Current);
        rules.Resume(SessionKind.None, worldRunning: true);
        Assert.Equal(SessionKind.Unknown, rules.Current);
        rules.Resume(SessionKind.None, worldRunning: false);
        Assert.Equal(SessionKind.None, rules.Current);
    }
}
