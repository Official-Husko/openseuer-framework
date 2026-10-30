using CheatMenu.Dev;
using ObenLoader.Logging;

namespace ObenCore.Tests.Cheats;

public class LogViewTests
{
    private static LogBuffer Buffer(params (LogLevel Level, string Source, string Message)[] lines)
    {
        var buffer = new LogBuffer(64);
        foreach (var line in lines)
            buffer.Add(line.Level, line.Source, line.Message);
        return buffer;
    }

    private static string[] Messages(LogView view) => view.Lines.Select(e => e.Message).ToArray();

    [Fact]
    public void KeepsLinesAtOrAboveTheLevel()
    {
        var buffer = Buffer((LogLevel.Debug, "a", "debug"), (LogLevel.Info, "a", "info"), (LogLevel.Warning, "a", "warn"), (LogLevel.Error, "a", "error"));
        var view = new LogView { MinLevel = LogLevel.Warning };
        Assert.Equal(2, view.Pull(buffer));
        Assert.Equal(new[] { "warn", "error" }, Messages(view));
    }

    [Fact]
    public void SourceMatchesWholeNamesInAnyCase()
    {
        var buffer = Buffer((LogLevel.Info, "ObenCoop", "one"), (LogLevel.Info, "ObenCoop.Net", "two"), (LogLevel.Info, "Unity", "three"));
        var view = new LogView { Source = "obencoop" };
        view.Pull(buffer);
        Assert.Equal(new[] { "one" }, Messages(view));
    }

    [Fact]
    public void FilterLooksAtTheMessageAndTheSource()
    {
        var buffer = Buffer((LogLevel.Info, "ObenCoop", "joined"), (LogLevel.Info, "Unity", "Lobby created"), (LogLevel.Info, "Unity", "other"));
        var view = new LogView { Filter = "LOBBY" };
        view.Pull(buffer);
        Assert.Equal(new[] { "Lobby created" }, Messages(view));
        view.Filter = "coop";
        view.Reset();
        view.Pull(buffer);
        Assert.Equal(new[] { "joined" }, Messages(view));
    }

    [Fact]
    public void PullTakesOnlyNewLines()
    {
        var buffer = Buffer((LogLevel.Info, "a", "one"));
        var view = new LogView();
        Assert.Equal(1, view.Pull(buffer));
        Assert.Equal(0, view.Pull(buffer));
        buffer.Add(LogLevel.Debug, "a", "hidden");
        Assert.Equal(0, view.Pull(buffer));
        buffer.Add(LogLevel.Info, "a", "two");
        Assert.Equal(1, view.Pull(buffer));
        Assert.Equal(new[] { "one", "two" }, Messages(view));
    }

    [Fact]
    public void ResetChecksEveryLineAgain()
    {
        var buffer = Buffer((LogLevel.Debug, "a", "debug"), (LogLevel.Info, "a", "info"));
        var view = new LogView();
        view.Pull(buffer);
        view.MinLevel = LogLevel.Debug;
        view.Reset();
        view.Pull(buffer);
        Assert.Equal(new[] { "debug", "info" }, Messages(view));
    }

    [Fact]
    public void IndexOfFindsALineBySequence()
    {
        var buffer = Buffer((LogLevel.Info, "a", "0"), (LogLevel.Debug, "a", "1"), (LogLevel.Info, "a", "2"), (LogLevel.Info, "a", "3"));
        var view = new LogView();
        view.Pull(buffer);
        Assert.Equal(0, view.IndexOf(0));
        Assert.Equal(-1, view.IndexOf(1));
        Assert.Equal(1, view.IndexOf(2));
        Assert.Equal(2, view.IndexOf(3));
        Assert.Equal(-1, view.IndexOf(9));
    }

    [Fact]
    public void SourcesAreDistinctAndSorted()
    {
        var buffer = Buffer((LogLevel.Info, "Unity", "x"), (LogLevel.Debug, "CheatMenu", "y"), (LogLevel.Info, "Unity", "z"), (LogLevel.Info, "", "w"));
        Assert.Equal(new[] { "CheatMenu", "Unity" }, LogView.Sources(buffer).ToArray());
    }

    [Fact]
    public void RowShowsTheFirstLineCutAndExtraLinesAreCounted()
    {
        var entry = new LogEntry(0, new DateTime(2026, 10, 30, 13, 5, 9), LogLevel.Error, "Mod", "Boom\r\n  at A()\n  at B()\n");
        Assert.Equal("13:05:09 Mod: Boom", LogView.Row(entry));
        Assert.Equal(2, LogView.ExtraLines(entry));

        var longEntry = new LogEntry(1, DateTime.Now, LogLevel.Info, "Mod", new string('x', 500));
        Assert.Equal(LogView.RowLength, LogView.Row(longEntry).Length);
        Assert.EndsWith("...", LogView.Row(longEntry));
    }
}
