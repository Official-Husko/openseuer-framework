using ObenLoader.Logging;

namespace ObenCore.Tests;

public class LogBufferTests
{
    private static LogBuffer Filled(int capacity, int count)
    {
        var buffer = new LogBuffer(capacity);
        for (int i = 0; i < count; i++)
            buffer.Add(LogLevel.Info, "test", "line " + i);
        return buffer;
    }

    private static long[] Sequences(IEnumerable<LogEntry> entries) => entries.Select(e => e.Sequence).ToArray();

    [Fact]
    public void EmptyBufferHasNoEntries()
    {
        var buffer = new LogBuffer(4);
        Assert.Equal(-1, buffer.LastSequence);
        Assert.Empty(buffer.Read(-1, 10));
    }

    [Fact]
    public void SequenceNumbersStartAtZeroAndCountUp()
    {
        var buffer = new LogBuffer(4);
        Assert.Equal(0, buffer.Add(LogLevel.Info, "a", "one"));
        Assert.Equal(1, buffer.Add(LogLevel.Info, "a", "two"));
        Assert.Equal(1, buffer.LastSequence);
    }

    [Fact]
    public void ReadsEntriesOldestFirst()
    {
        var entries = Filled(8, 3).Read(-1, 10);
        Assert.Equal(new long[] { 0, 1, 2 }, Sequences(entries));
        Assert.Equal("line 0", entries[0].Message);
        Assert.Equal("test", entries[0].Source);
    }

    [Fact]
    public void WraparoundKeepsOnlyTheNewestEntries()
    {
        var buffer = Filled(3, 5);
        Assert.Equal(4, buffer.LastSequence);
        Assert.Equal(new long[] { 2, 3, 4 }, Sequences(buffer.Read(-1, 10)));
        Assert.Equal("line 2", buffer.Read(-1, 10)[0].Message);
    }

    [Fact]
    public void ManyWrapsStayConsistent()
    {
        var buffer = Filled(4, 103);
        Assert.Equal(new long[] { 99, 100, 101, 102 }, Sequences(buffer.Read(-1, 10)));
    }

    [Fact]
    public void ReadAfterReturnsOnlyNewerEntries()
    {
        var buffer = Filled(10, 5);
        Assert.Equal(new long[] { 3, 4 }, Sequences(buffer.Read(2, 10)));
        Assert.Empty(buffer.Read(4, 10));
        Assert.Empty(buffer.Read(100, 10));
    }

    [Fact]
    public void ReadAfterAnEvictedSequenceStartsAtTheOldestKept()
    {
        var buffer = Filled(3, 10);
        Assert.Equal(new long[] { 7, 8, 9 }, Sequences(buffer.Read(2, 10)));
    }

    [Fact]
    public void MaxKeepsTheNewestEntriesInOrder()
    {
        var buffer = Filled(10, 6);
        Assert.Equal(new long[] { 4, 5 }, Sequences(buffer.Read(-1, 2)));
        Assert.Empty(buffer.Read(-1, 0));
    }

    [Fact]
    public void LevelFilterSkipsLowerLevels()
    {
        var buffer = new LogBuffer(10);
        buffer.Add(LogLevel.Debug, "s", "d");
        buffer.Add(LogLevel.Info, "s", "i");
        buffer.Add(LogLevel.Warning, "s", "w");
        buffer.Add(LogLevel.Error, "s", "e");

        Assert.Equal(new long[] { 2, 3 }, Sequences(buffer.Read(-1, 10, LogLevel.Warning)));
        Assert.Equal(new long[] { 3 }, Sequences(buffer.Read(-1, 10, LogLevel.Error)));
        Assert.Equal(4, buffer.Read(-1, 10).Count);
    }

    [Fact]
    public void MaxCountsOnlyEntriesThatPassTheLevelFilter()
    {
        var buffer = new LogBuffer(10);
        buffer.Add(LogLevel.Error, "s", "old error");
        for (int i = 0; i < 5; i++)
            buffer.Add(LogLevel.Debug, "s", "noise");

        var result = buffer.Read(-1, 1, LogLevel.Error);
        Assert.Single(result);
        Assert.Equal("old error", result[0].Message);
    }

    [Fact]
    public void ConcurrentWritersGetUniqueSequences()
    {
        var buffer = new LogBuffer(1000);
        var seen = new System.Collections.Concurrent.ConcurrentBag<long>();
        Parallel.For(0, 800, _ => seen.Add(buffer.Add(LogLevel.Info, "s", "m")));
        Assert.Equal(800, seen.Distinct().Count());
        Assert.Equal(799, buffer.LastSequence);
    }
}
