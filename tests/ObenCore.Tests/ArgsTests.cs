using ObenTool.Cli;

namespace ObenCore.Tests;

public class ArgsTests
{
    private static Args Parse(params string[] raw) => new(raw);

    [Fact]
    public void PositionalArgumentsKeepTheirOrder()
    {
        var args = Parse("one", "two", "three");
        Assert.Equal(new[] { "one", "two", "three" }, args.Positional);
        Assert.Equal("two", args.At(1));
        Assert.Null(args.At(3));
    }

    [Fact]
    public void OptionTakesTheNextValue()
    {
        var args = Parse("--name", "checks", "file.cs");
        Assert.Equal("checks", args.Get("name"));
        Assert.Equal(new[] { "file.cs" }, args.Positional);
    }

    [Fact]
    public void EqualsFormWorksAndKeepsLaterEquals()
    {
        var args = Parse("--game=/a/b", "--expr=x=y");
        Assert.Equal("/a/b", args.Get("game"));
        Assert.Equal("x=y", args.Get("expr"));
    }

    [Fact]
    public void FlagWithoutValueIsPresentButNull()
    {
        var args = Parse("--verbose");
        Assert.True(args.Has("verbose"));
        Assert.Null(args.Get("verbose"));
        Assert.Equal("fallback", args.Get("verbose", "fallback"));
    }

    [Fact]
    public void FlagBeforeAnotherOptionTakesNoValue()
    {
        var args = Parse("--verbose", "--game", "/g");
        Assert.True(args.Has("verbose"));
        Assert.Null(args.Get("verbose"));
        Assert.Equal("/g", args.Get("game"));
    }

    [Fact]
    public void TrailingFlagIsPresent()
    {
        var args = Parse("code", "--json");
        Assert.True(args.Has("json"));
        Assert.Equal(new[] { "code" }, args.Positional);
    }

    [Fact]
    public void OptionNamesIgnoreCase()
    {
        var args = Parse("--Game", "/g");
        Assert.True(args.Has("game"));
        Assert.Equal("/g", args.Get("GAME"));
    }

    [Fact]
    public void MissingOptionFallsBack()
    {
        var args = Parse();
        Assert.False(args.Has("x"));
        Assert.Null(args.Get("x"));
        Assert.Equal("d", args.Get("x", "d"));
    }

    [Fact]
    public void LaterOptionOverridesEarlier()
    {
        Assert.Equal("2", Parse("--n", "1", "--n", "2").Get("n"));
    }

    [Fact]
    public void ValueStartingWithOneDashIsAValue()
    {
        var args = Parse("--offset", "-5");
        Assert.Equal(-5, args.GetInt("offset", 0));
    }

    [Fact]
    public void GetIntFallsBackOnMissingOrBadValues()
    {
        var args = Parse("--n", "abc", "--m", "12");
        Assert.Equal(7, args.GetInt("n", 7));
        Assert.Equal(7, args.GetInt("absent", 7));
        Assert.Equal(12, args.GetInt("m", 7));
    }

    [Fact]
    public void InstanceDefaultsToOne()
    {
        var args = Parse();
        Assert.Equal(1, args.Instance);
        Assert.Null(args.InstanceFilter);
    }

    [Fact]
    public void InstanceIsReadFromTheOption()
    {
        var args = Parse("--instance", "3");
        Assert.Equal(3, args.Instance);
        Assert.Equal(3, args.InstanceFilter);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("two")]
    public void BadInstanceThrows(string value)
    {
        Assert.Throws<ArgumentException>(() => Parse("--instance", value).Instance);
    }

    [Fact]
    public void BridgePortFollowsTheInstance()
    {
        Assert.Equal(27860, Parse().BridgePort);
        Assert.Equal(27860, Parse("--instance", "1").BridgePort);
        Assert.Equal(27862, Parse("--instance", "3").BridgePort);
    }

    [Fact]
    public void ExplicitPortWinsOverInstance()
    {
        Assert.Equal(1234, Parse("--instance", "3", "--port", "1234").BridgePort);
    }
}
