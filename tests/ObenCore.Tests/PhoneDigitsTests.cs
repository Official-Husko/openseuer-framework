using ObenFramework.Npcs;

namespace ObenCore.Tests;

public class PhoneDigitsTests
{
    [Theory]
    [InlineData("112", new[] { 1, 1, 2 })]
    [InlineData("*#0", new[] { 10, 11, 0 })]
    [InlineData("0123456789", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })]
    public void NumbersParseToKeyCodes(string text, int[] keys)
    {
        Assert.True(PhoneDigits.TryParse(text, out var parsed));
        Assert.Equal(keys, parsed);
        Assert.Equal(text, PhoneDigits.Format(parsed));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12345678901")]
    [InlineData("12a")]
    [InlineData("+358")]
    public void BadNumbersAreRefused(string? text) => Assert.False(PhoneDigits.TryParse(text, out _));

    [Fact]
    public void SameComparesKeyByKey()
    {
        Assert.True(PhoneDigits.Same(new[] { 1, 2 }, new[] { 1, 2 }));
        Assert.False(PhoneDigits.Same(new[] { 1, 2 }, new[] { 1, 3 }));
        Assert.False(PhoneDigits.Same(new[] { 1, 2 }, new[] { 1 }));
        Assert.False(PhoneDigits.Same(null, new[] { 1 }));
        Assert.Equal(string.Empty, PhoneDigits.Format(null));
    }
}
