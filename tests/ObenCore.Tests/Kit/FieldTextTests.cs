using ObenUIKit.Menu.Settings;

namespace ObenCore.Tests.Kit;

public class FieldTextTests
{
    [Theory]
    [InlineData("PauseWhileOpen", "Pause while open")]
    [InlineData("speed", "Speed")]
    [InlineData("Max_Players", "Max Players")]
    [InlineData("HUDScale", "HUDScale")]
    public void FieldNamesBecomeWords(string name, string expected) => Assert.Equal(expected, FieldText.Prettify(name));

    [Fact]
    public void ValuesAreWrittenForThePlayer()
    {
        Assert.Equal("On", FieldText.Describe(true));
        Assert.Equal("Off", FieldText.Describe(false));
        Assert.Equal("2.5", FieldText.Describe(2.5f));
        Assert.Equal("0.333", FieldText.Describe(1.0 / 3.0));
        Assert.Equal("4", FieldText.Describe(4));
        Assert.Equal("empty", FieldText.Describe(""));
        Assert.Equal("Day of week", FieldText.Describe(Kind.DayOfWeek));
        Assert.Equal("a, b", FieldText.Describe(new List<string> { "a", "b" }));
        Assert.Equal("empty", FieldText.Describe(new string[0]));
    }

    [Fact]
    public void ListsSplitOnCommasAndDropEmptyParts()
    {
        Assert.Equal(new[] { "a", "b c" }, FieldText.SplitList(" a, , b c ,"));
        Assert.Empty(FieldText.SplitList(""));
        Assert.Empty(FieldText.SplitList(null));
        Assert.Equal("a, b c", FieldText.JoinList(FieldText.SplitList("a,b c")));
    }

    [Theory]
    [InlineData("1, 2.5, -3", 3, true)]
    [InlineData("1 2", 2, true)]
    [InlineData("1,2", 3, false)]
    [InlineData("1,x", 2, false)]
    [InlineData("NaN, 1", 2, false)]
    [InlineData("", 2, false)]
    public void VectorsReadExactlyTheirNumbers(string text, int count, bool ok)
    {
        Assert.Equal(ok, FieldText.TryNumbers(text, count, out var values));
        if (ok)
            Assert.Equal(count, values.Length);
    }

    [Fact]
    public void NumbersRoundTrip()
    {
        Assert.True(FieldText.TryNumbers(FieldText.JoinNumbers(new[] { 1f, 2.25f, -0.5f }), 3, out var values));
        Assert.Equal(new[] { 1f, 2.25f, -0.5f }, values);
    }

    [Theory]
    [InlineData("third key", "Third-person key\nGame tweaks", true)]
    [InlineData("THIRD", "Third-person key", true)]
    [InlineData("third mouse", "Third-person key", false)]
    [InlineData("", "anything", true)]
    [InlineData("  ", null, true)]
    [InlineData("x", null, false)]
    public void SearchNeedsEveryWord(string query, string text, bool expected) =>
        Assert.Equal(expected, FieldText.Matches(query, text));

    [Fact]
    public void SearchTextSkipsMissingParts() => Assert.Equal("Label\nGroup\n", FieldText.SearchText("Label", null, "", "Group"));

    [Fact]
    public void AValueOutsideTheChoicesIsOfferedFirst()
    {
        var choices = new[] { "friends", "public" };
        Assert.Same(choices, FieldText.WithCurrent(choices, "public", out int index));
        Assert.Equal(1, index);
        Assert.Equal(new[] { "invite", "friends", "public" }, FieldText.WithCurrent(choices, "invite", out index));
        Assert.Equal(0, index);
        Assert.Same(choices, FieldText.WithCurrent(choices, null, out index));
        Assert.Equal(0, index);
    }

    private enum Kind { DayOfWeek }
}
