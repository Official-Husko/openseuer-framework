using ObenFramework.Saves;

namespace ObenCore.Tests;

public class SaveBlobLimitsTests
{
    private const string Door = "{\"GUID\":\"APT_7_a\",\"Data\":{\"State\":\"Closed\",\"saved\":true,\"$type\":\"Door\"}}";

    [Fact]
    public void AcceptsAnEntryTheGameWrote()
    {
        Assert.Null(SaveBlobLimits.RefuseReason(Door));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RefusesEmptyText(string? json)
    {
        Assert.NotNull(SaveBlobLimits.RefuseReason(json));
    }

    [Fact]
    public void RefusesOversizedText()
    {
        var big = new string(' ', SaveBlobLimits.MaxChars + 1);
        Assert.Contains("character limit", SaveBlobLimits.RefuseReason(big));
        Assert.Null(SaveBlobLimits.RefuseReason(new string(' ', SaveBlobLimits.MaxChars)));
    }

    [Fact]
    public void AllowsNestingUpToTheLimitOnly()
    {
        var atLimit = new string('[', SaveBlobLimits.MaxDepth) + new string(']', SaveBlobLimits.MaxDepth);
        var over = new string('[', SaveBlobLimits.MaxDepth + 1) + new string(']', SaveBlobLimits.MaxDepth + 1);
        Assert.Null(SaveBlobLimits.RefuseReason(atLimit));
        Assert.Contains("nested deeper", SaveBlobLimits.RefuseReason(over));
    }

    [Fact]
    public void RefusesUnterminatedDeepNesting()
    {
        Assert.NotNull(SaveBlobLimits.RefuseReason(new string('[', 10000)));
    }

    [Fact]
    public void RefusesCloserWithoutOpener()
    {
        Assert.Contains("closes more", SaveBlobLimits.RefuseReason("]"));
        Assert.Contains("closes more", SaveBlobLimits.RefuseReason("{}}"));
    }

    [Theory]
    [InlineData("[/*\"*/[[[[")]
    [InlineData("[/*]]]]*/[[[[")]
    [InlineData("{} // comment")]
    public void RefusesComments(string json)
    {
        Assert.Contains("comments", SaveBlobLimits.RefuseReason(json));
    }

    [Fact]
    public void BracketsAndSlashesInsideStringsDoNotCount()
    {
        Assert.Null(SaveBlobLimits.RefuseReason("[\"a/b\",\"]]]\",\"\\\"[[\"]"));
    }

    [Fact]
    public void EscapedQuoteDoesNotEndTheString()
    {
        // The slash sits inside the string because \" does not close it.
        Assert.Null(SaveBlobLimits.RefuseReason("[\"x\\\" / y\"]"));
    }
}
