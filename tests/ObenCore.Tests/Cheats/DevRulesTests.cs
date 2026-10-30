using CheatMenu.Dev;

namespace ObenCore.Tests.Cheats;

public class DevRulesTests
{
    [Theory]
    [InlineData("TenementBasementDone", "", true)]
    [InlineData("TenementBasementDone", null, true)]
    [InlineData("TenementBasementDone", "basement", true)]
    [InlineData("TenementBasementDone", "  basement ", true)]
    [InlineData("TenementBasementDone", "roof", false)]
    [InlineData(null, "roof", false)]
    public void MatchesIgnoresCaseAndSpaces(string? text, string? filter, bool expected) =>
        Assert.Equal(expected, DevRules.Matches(text, filter));

    [Fact]
    public void PageShowsAtMostMaxButCountsEveryMatch()
    {
        var all = new[] { "a1", "b1", "a2", "a3", "a4" };
        var shown = DevRules.Page(all, "a", 2, out int matched);
        Assert.Equal(new[] { "a1", "a2" }, shown);
        Assert.Equal(4, matched);
    }

    [Theory]
    [InlineData("DevBridge.test", "DevBridge")]
    [InlineData("My.Mod.intro", "My.Mod")]
    [InlineData("Abandoned2", null)]
    [InlineData("Assets/Content/Tasks/Intro.asset", null)]
    [InlineData(".intro", null)]
    [InlineData("MyMod.", null)]
    [InlineData("MyMod.2nd", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TaskOwnerIsTheModIdBeforeTheLastDot(string? taskId, string? expected) =>
        Assert.Equal(expected, DevRules.TaskOwner(taskId));

    [Fact]
    public void SplitPathTrimsEachName()
    {
        var parts = DevRules.SplitPath(" currentTimeAndDay . hours ", out string error);
        Assert.Null(error);
        Assert.Equal(new[] { "currentTimeAndDay", "hours" }, parts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a..b")]
    [InlineData("a.b-c")]
    [InlineData("1abc")]
    [InlineData("a.b.c.d.e.f.g.h.i")]
    public void SplitPathRejectsWhatIsntAPath(string path)
    {
        Assert.Null(DevRules.SplitPath(path, out string error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void WatchKeysRoundTrip()
    {
        string key = DevRules.WatchKey(" TimeOfDayAzure ", " currentTimeAndDay.hours");
        Assert.Equal("TimeOfDayAzure:currentTimeAndDay.hours", key);
        Assert.True(DevRules.TrySplitWatch(key, out string type, out string path));
        Assert.Equal("TimeOfDayAzure", type);
        Assert.Equal("currentTimeAndDay.hours", path);
    }

    [Theory]
    [InlineData("TimeOfDayAzure")]
    [InlineData(":hours")]
    [InlineData("TimeOfDayAzure:")]
    [InlineData("TimeOfDayAzure:a b")]
    [InlineData(null)]
    public void BrokenWatchKeysAreRefused(string? key) => Assert.False(DevRules.TrySplitWatch(key, out _, out _));

    [Theory]
    [InlineData("short", 10, "short")]
    [InlineData("exactly10!", 10, "exactly10!")]
    [InlineData("a bit too long", 10, "a bit t...")]
    [InlineData(null, 10, "")]
    public void CutKeepsTheLimit(string? text, int max, string expected) => Assert.Equal(expected, DevRules.Cut(text, max));

    [Theory]
    [InlineData("12.5", 12.5f)]
    [InlineData(" -3,25 ", -3.25f)]
    [InlineData("1e2", 100f)]
    public void NumbersTakeAPointOrAComma(string text, float expected)
    {
        Assert.True(DevRules.TryParseNumber(text, out float value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1.2.3")]
    public void NumbersRefuseTheRest(string? text) => Assert.False(DevRules.TryParseNumber(text!, out _));

    [Fact]
    public void AVectorNeedsAllThree()
    {
        Assert.True(DevRules.TryParseVector("1", "2.5", "-3", out float x, out float y, out float z));
        Assert.Equal((1f, 2.5f, -3f), (x, y, z));
        Assert.False(DevRules.TryParseVector("1", "", "3", out _, out _, out _));
    }
}
