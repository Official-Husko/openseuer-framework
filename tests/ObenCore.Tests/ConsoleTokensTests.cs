using ObenFramework.Console;

namespace ObenCore.Tests;

public class ConsoleTokensTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyInputGivesNoTokens(string? line)
    {
        Assert.Empty(ConsoleTokens.Split(line));
    }

    [Fact]
    public void SplitsOnSpaces()
    {
        Assert.Equal(new[] { "give", "apple", "3" }, ConsoleTokens.Split("give apple 3"));
    }

    [Fact]
    public void RepeatedAndSurroundingSpacesAreDropped()
    {
        Assert.Equal(new[] { "a", "b" }, ConsoleTokens.Split("   a    b  "));
    }

    [Fact]
    public void QuotedPartsStayWhole()
    {
        Assert.Equal(new[] { "say", "hello world", "now" }, ConsoleTokens.Split("say \"hello world\" now"));
    }

    [Fact]
    public void QuoteAtTheStartAndEnd()
    {
        Assert.Equal(new[] { "one two", "three four" }, ConsoleTokens.Split("\"one two\" \"three four\""));
    }

    [Fact]
    public void QuotedSpacesArePreserved()
    {
        Assert.Equal(new[] { "x", "  padded  " }, ConsoleTokens.Split("x \"  padded  \""));
    }

    [Fact]
    public void UnterminatedQuoteTakesTheRestOfTheLine()
    {
        Assert.Equal(new[] { "a", "b c" }, ConsoleTokens.Split("a \"b c"));
    }

    [Fact]
    public void EmptyQuotesGiveAnEmptyToken()
    {
        Assert.Equal(new[] { "a", "", "b" }, ConsoleTokens.Split("a \"\" b"));
    }

    [Theory]
    [InlineData("report", true)]
    [InlineData("mod_cmd-2", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("two words", false)]
    [InlineData("tab\there", false)]
    [InlineData("say\"", false)]
    public void CommandNamesAreOneWordWithoutQuotes(string? name, bool valid)
    {
        Assert.Equal(valid, ConsoleTokens.IsName(name!));
    }
}
