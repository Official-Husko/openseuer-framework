using DevBridge.Tracing;

namespace ObenCore.Tests.Tracing;

public class TraceBookTests
{
    private static void Call(TraceBook book, TraceStats stats, double now, bool failed = false)
    {
        if (book.Admit(stats, 0.5, failed, now))
            book.AddCall(new TraceRecord { Method = stats.Method, Time = now });
    }

    [Fact]
    public void KeepsTheFirstCallsOfAWindowAndCountsTheRest()
    {
        var book = new TraceBook(capacity: 100, linesPerWindow: 3, windowSeconds: 10);
        var stats = book.StatsFor("GameClock.Hour", 0);
        for (int i = 0; i < 10; i++)
            Call(book, stats, 1);

        Assert.Equal(3, book.Read(0, 100).Count);
        Assert.Equal(10, stats.Calls);
        Assert.False(book.WantsDetail(stats, 2));
        Assert.True(book.WantsDetail(stats, 11));
    }

    [Fact]
    public void SweepWritesOneMoreRecordPerFullWindow()
    {
        var book = new TraceBook(capacity: 100, linesPerWindow: 2, windowSeconds: 10);
        var stats = book.StatsFor("StatDrain.Apply", 0);
        var parameters = typeof(Math).GetMethod("Max", new[] { typeof(int), typeof(int) })!.GetParameters();
        for (int i = 0; i < 7; i++)
        {
            if (book.Admit(stats, 0.1, false, 1, parameters, new object[] { i, 9 }, isVoid: false, result: 9))
                book.AddCall(new TraceRecord { Method = stats.Method });
        }

        book.Sweep(5);
        Assert.Equal(2, book.Read(0, 100).Count);

        book.Sweep(10.5);
        var records = book.Read(0, 100);
        Assert.Equal(3, records.Count);
        var more = records[2];
        Assert.Equal(TraceBook.MoreKind, more.Kind);
        Assert.Equal(5, more.More);
        Assert.Equal("6, 9", more.Args);
        Assert.Equal("9", more.Result);

        // The window started over: the next calls are kept in full again.
        Call(book, stats, 11);
        Assert.Equal(4, book.Read(0, 100).Count);
    }

    [Fact]
    public void ErrorsAreAlwaysKept()
    {
        var book = new TraceBook(capacity: 100, linesPerWindow: 1, windowSeconds: 10);
        var stats = book.StatsFor("QuestLog.Start", 0);
        Call(book, stats, 1);
        Call(book, stats, 1);
        Call(book, stats, 1, failed: true);
        Assert.Equal(2, book.Read(0, 100).Count);
        Assert.Equal(1, stats.Errors);
    }

    [Fact]
    public void ErrorsPastTheLinesHaveACapOfTheirOwn()
    {
        var book = new TraceBook(capacity: 100, linesPerWindow: 2, windowSeconds: 10);
        var stats = book.StatsFor("Npcs.Spawn", 0);
        for (int i = 0; i < 3; i++)
            Call(book, stats, 1);
        for (int i = 0; i < 10; i++)
            Call(book, stats, 1, failed: true);

        // Two calls, then two of the errors; the other nine go into the window's "more" record.
        Assert.Equal(4, book.Read(0, 100).Count);
        Assert.Equal(10, stats.Errors);
        book.Sweep(10.5);
        Assert.Equal(9, book.Read(0, 100)[4].More);
    }

    [Fact]
    public void TheRingDropsTheOldest()
    {
        var book = new TraceBook(capacity: 16, linesPerWindow: 1000, windowSeconds: 10);
        var stats = book.StatsFor("A.B", 0);
        for (int i = 0; i < 40; i++)
            Call(book, stats, 1);

        var records = book.Read(0, 100);
        Assert.Equal(16, records.Count);
        Assert.Equal(25, records[0].Seq);
        Assert.Equal(40, records[15].Seq);
        Assert.Equal(41, book.NextSeq);
    }

    [Fact]
    public void ReadsAfterASequenceNumberWithAMaxAndAMethodFilter()
    {
        var book = new TraceBook(capacity: 100, linesPerWindow: 100, windowSeconds: 10);
        var a = book.StatsFor("QuestLog.Start", 0);
        var b = book.StatsFor("GlobalFlags.Set", 0);
        for (int i = 0; i < 5; i++)
        {
            Call(book, a, 1);
            Call(book, b, 1);
        }
        book.AddMark("scene loaded: Tenement", 2, 100);

        Assert.Equal(new long[] { 9, 10, 11 }, book.Read(8, 100).Select(r => r.Seq));
        Assert.Equal(2, book.Read(0, 2).Count);
        Assert.All(book.Read(0, 100, "flags"), r => Assert.Equal("GlobalFlags.Set", r.Method));
        Assert.Equal(TraceBook.MarkKind, book.Read(10, 100).Single().Kind);
    }

    [Fact]
    public void TopIsOrderedByCalls()
    {
        var book = new TraceBook();
        var a = book.StatsFor("A.Rare", 0);
        var b = book.StatsFor("B.Often", 0);
        book.StatsFor("C.Never", 0);
        Call(book, a, 1);
        for (int i = 0; i < 3; i++)
            Call(book, b, 1);

        var top = book.Top(10);
        Assert.Equal(new[] { "B.Often", "A.Rare" }, top.Select(s => s.Method));
        Assert.Single(book.Top(1));
    }

    [Fact]
    public void ClearKeepsCountingSequenceNumbers()
    {
        var book = new TraceBook();
        var stats = book.StatsFor("A.B", 0);
        Call(book, stats, 1);
        book.Clear();
        Assert.Empty(book.Read(0, 100));
        Call(book, book.StatsFor("A.B", 0), 1);
        Assert.Equal(2, book.Read(0, 100).Single().Seq);
    }
}
