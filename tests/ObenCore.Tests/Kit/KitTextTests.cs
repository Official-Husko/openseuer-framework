using ObenUIKit.Controls;

namespace ObenCore.Tests.Kit;

public class KitTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("plain text", "plain text")]
    [InlineData("a > b", "a > b")]
    [InlineData("List<int>", "List<noparse><</noparse>int>")]
    [InlineData("<color=red>x</color>", "<noparse><</noparse>color=red>x<noparse><</noparse>/color>")]
    public void EscapeKeepsTextAsItIs(string? text, string expected)
    {
        KitText.BracketFont = null;
        Assert.Equal(expected, KitText.Escape(text!));
    }

    [Fact]
    public void BracketsUseTheBracketFont()
    {
        KitText.BracketFont = "Sans";
        try
        {
            Assert.Equal("MODS <font=\"Sans\">></font> Cheats", KitText.Escape("MODS > Cheats"));
            Assert.Equal("a <font=\"Sans\"><noparse><</noparse></font> b", KitText.Escape("a < b"));
        }
        finally
        {
            KitText.BracketFont = null;
        }
    }

    [Fact]
    public void PaintSkipsEmptyText()
    {
        Assert.Equal("", KitText.Paint("#FFFFFF", ""));
        Assert.Equal("<color=#FFFFFF>hi</color>", KitText.Paint("#FFFFFF", "hi"));
    }

    [Theory]
    [InlineData(2.5f, "0.00", "2.50")]
    [InlineData(1234.5f, "0.0", "1234.5")]
    [InlineData(-0.001f, "0.00", "0.00")]
    [InlineData(-0.5f, "0.0", "-0.5")]
    [InlineData(float.NaN, "0", "-")]
    [InlineData(float.PositiveInfinity, "0", "-")]
    public void NumbersReadTheSameEverywhere(float value, string format, string expected)
    {
        var before = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            Assert.Equal(expected, KitText.Number(value, format));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = before;
        }
    }
}
