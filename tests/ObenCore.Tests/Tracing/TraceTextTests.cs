using DevBridge.Tracing;

namespace ObenCore.Tests.Tracing;

public class TraceTextTests
{
    [Fact]
    public void CallsAreIndentedByDepthWithTheirTimeThreadAndCaller()
    {
        var record = new TraceRecord
        {
            Kind = TraceBook.CallKind, Time = 1.5, Frame = 42, Method = "GameClock.SetTime", Args = "hours: 13, minutes: 47",
            Result = "void", Ms = 0.0123, Depth = 2, Thread = 7, From = "CheatMenu",
        };
        Assert.Equal("    1.500 f42      GameClock.SetTime(hours: 13, minutes: 47) -> void (0.012 ms) [thread 7] from CheatMenu", TraceText.Line(record));
    }

    [Fact]
    public void AFailedCallSaysWhatItThrew()
    {
        var record = new TraceRecord { Kind = TraceBook.CallKind, Method = "Wallet.Add", Args = "-5", Error = "ArgumentException: no", Ms = 1 };
        Assert.EndsWith("Wallet.Add(-5) threw ArgumentException: no (1.000 ms)", TraceText.Line(record));
    }

    [Fact]
    public void MarksAndMoreNotesReadPlainly()
    {
        Assert.EndsWith("--- scene loaded: Tenement", TraceText.Line(new TraceRecord { Kind = TraceBook.MarkKind, Method = "scene loaded: Tenement" }));
        Assert.EndsWith("GameClock.Hour: 118 more calls, the last () -> 13",
            TraceText.Line(new TraceRecord { Kind = TraceBook.MoreKind, Method = "GameClock.Hour", More = 118, Args = "", Result = "13" }));
        Assert.EndsWith("GameClock.Hour: 1 more call", TraceText.Line(new TraceRecord { Kind = TraceBook.MoreKind, Method = "GameClock.Hour", More = 1 }));
    }

    [Fact]
    public void DeepNestingIsCapped()
    {
        string deep = TraceText.Line(new TraceRecord { Kind = TraceBook.CallKind, Method = "A", Depth = 500 });
        Assert.Contains("f0  " + new string(' ', TraceText.MaxIndent * 2) + "A(", deep);
        Assert.DoesNotContain("f0  " + new string(' ', TraceText.MaxIndent * 2 + 1) + "A(", deep);
    }

    [Fact]
    public void WriteHasTheHeadingRecordsAndTheMostCalled()
    {
        var book = new TraceBook();
        book.AddMark("trace started", 0, 1);
        var stats = book.StatsFor("GameClock.Hour", 0);
        book.Admit(stats, 0.5, false, 0);
        book.Admit(stats, 1.5, false, 0);
        var writer = new StringWriter();
        TraceText.Write(writer, "API trace", book.Read(0, 100), book.Top(5));
        string text = writer.ToString();
        Assert.StartsWith("API trace", text);
        Assert.Contains("--- trace started", text);
        Assert.Contains("GameClock.Hour x2, 1.0000 ms on average, 1.500 ms at most", text);
    }

    [Fact]
    public void LineGateLetsThroughSoManyASecondAndCountsTheRest()
    {
        var gate = new LineGate(2);
        Assert.True(gate.Take(10.0));
        Assert.True(gate.Take(10.2));
        Assert.False(gate.Take(10.5));
        Assert.False(gate.Take(10.9));
        Assert.Equal(2, gate.TakeDropped());
        Assert.Equal(0, gate.TakeDropped());
        Assert.True(gate.Take(11.0));
    }
}
