using ObenLoader.Logging;

namespace ObenCore.Tests;

public class ModLogFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oben-modlogs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private static LogEntry Entry(string source, string message, LogLevel level = LogLevel.Info) =>
        new LogEntry(1, new DateTime(2026, 10, 29, 12, 0, 1, 250), level, source, message);

    private string Read(string name)
    {
        using var stream = new FileStream(Path.Combine(_dir, name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("MyMod", "MyMod")]
    [InlineData("a/b:c", "a_b_c")]
    [InlineData("con", "_con")]
    [InlineData("nul.json", "_nul.json")]
    [InlineData("trailing. ", "trailing")]
    [InlineData("", "unnamed")]
    [InlineData("...", "unnamed")]
    public void FileNamesAreSafe(string source, string expected) => Assert.Equal(expected, ModLogFiles.FileName(source));

    [Fact]
    public void LongNamesAreCut() => Assert.Equal(64, ModLogFiles.FileName(new string('x', 200)).Length);

    [Fact]
    public void EachSourceGetsItsOwnFileWithFrames()
    {
        using (var files = new ModLogFiles(_dir, ""))
        {
            files.Write(Entry("MyMod", "hello"), 812);
            files.Write(Entry("Other", "boom", LogLevel.Error), 813);
            files.Write(Entry("Unity", "not ours"), 814);
        }

        Assert.Equal("[12:00:01.250] [frame 812] [Info] hello" + Environment.NewLine, Read("MyMod.log"));
        Assert.Contains("[frame 813] [Error] boom", Read("Other.log"));
        Assert.False(File.Exists(Path.Combine(_dir, "Unity.log")));
    }

    [Fact]
    public void ThePreviousSessionsFileIsKept()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "MyMod.log"), "last time\n");
        using (var files = new ModLogFiles(_dir, "-2"))
            files.Write(Entry("MyMod", "this time"), 1);

        // A second instance has its own names; the first instance's file is left alone.
        Assert.Equal("last time\n", Read("MyMod.log"));
        Assert.Contains("this time", Read("MyMod-2.log"));

        using (var files = new ModLogFiles(_dir, ""))
            files.Write(Entry("MyMod", "and now"), 2);
        Assert.Equal("last time\n", Read("MyMod.previous.log"));
        Assert.Contains("and now", Read("MyMod.log"));
    }

    [Fact]
    public void ABigFileStartsOverAndKeepsItsEarlierPart()
    {
        string line = new string('x', 1000);
        using (var files = new ModLogFiles(_dir, ""))
        {
            for (int i = 0; i < ModLogFiles.RotateChars / 1000 + 10; i++)
                files.Write(Entry("Chatty", line), i);
            files.Write(Entry("Chatty", "after the turn"), 99_999);
        }

        Assert.True(new FileInfo(Path.Combine(_dir, "Chatty.previous.log")).Length >= ModLogFiles.RotateChars);
        Assert.True(new FileInfo(Path.Combine(_dir, "Chatty.log")).Length < ModLogFiles.RotateChars);
        Assert.Contains("after the turn", Read("Chatty.log"));
    }

    [Fact]
    public void AWriterThatCannotWriteTriesAgainLaterAndReportsOnce()
    {
        string path = Path.Combine(_dir, "later", "x.log");
        using var writer = new LogWriter(path, append: false, retryMs: 0);

        Assert.False(writer.WriteLine("lost", flush: true));
        Assert.Contains("x.log couldn't be written", writer.TakeFailure());
        Assert.False(writer.WriteLine("lost too", flush: true));
        Assert.Null(writer.TakeFailure());

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Assert.True(writer.WriteLine("back", flush: true));
        writer.Dispose();
        Assert.Equal("back" + Environment.NewLine, File.ReadAllText(path));
    }

    [Fact]
    public void SourcesWithTheSameFileNameShareOneFile()
    {
        using (var files = new ModLogFiles(_dir, ""))
        {
            files.Write(Entry("a/b", "first"), 1);
            files.Write(Entry("a_b", "second"), 2);
        }

        string text = Read("a_b.log");
        Assert.Contains("first", text);
        Assert.Contains("second", text);
    }
}
