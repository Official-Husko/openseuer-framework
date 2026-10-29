using ObenLoader.Mods;

namespace ObenCore.Tests;

public class SemVerTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3", true)]
    [InlineData("1.2.4", "1.2.3", true)]
    [InlineData("1.3.0", "1.2.9", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.2.2", "1.2.3", false)]
    [InlineData("1.1.9", "1.2.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    public void ComparesNumbers(string version, string minimum, bool expected)
    {
        Assert.Equal(expected, SemVer.Satisfies(version, minimum));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    public void NoMinimumAlwaysSatisfied(string? minimum)
    {
        Assert.True(SemVer.Satisfies("0.0.1", minimum));
        Assert.True(SemVer.Satisfies(null, minimum));
    }

    [Fact]
    public void TwoPartVersionsWork()
    {
        Assert.True(SemVer.Satisfies("1.2", "1.2"));
        Assert.True(SemVer.Satisfies("1.3", "1.2"));
        Assert.True(SemVer.Satisfies("1.2.1", "1.2"));
        Assert.False(SemVer.Satisfies("1.1", "1.2"));
    }

    [Fact]
    public void PreReleaseTagsAreIgnored()
    {
        Assert.True(SemVer.Satisfies("1.2.3-beta", "1.2.3"));
        Assert.True(SemVer.Satisfies("1.2.3", "1.2.3-rc1"));
        Assert.False(SemVer.Satisfies("1.2.2-beta", "1.2.3"));
    }

    [Theory]
    [InlineData(">=1.2.0")]
    [InlineData(">= 1.2.0")]
    [InlineData(">1.2.0")]
    public void LeadingOperatorsAreStripped(string minimum)
    {
        Assert.True(SemVer.Satisfies("1.2.0", minimum));
        Assert.False(SemVer.Satisfies("1.1.9", minimum));
    }

    [Fact]
    public void ALoneNumberIsAVersion()
    {
        Assert.True(SemVer.Satisfies("2", "1.5"));
        Assert.False(SemVer.Satisfies("1", "1.5"));
        Assert.True(SemVer.IsReadable("2"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("*", true)]
    [InlineData(">=1.2", true)]
    [InlineData("1.2.3-beta", true)]
    [InlineData("latest", false)]
    [InlineData("v1.2", false)]
    public void MinimumsAreReadableOrNot(string? minimum, bool expected) =>
        Assert.Equal(expected, SemVer.IsReadableMinimum(minimum!));

    [Fact]
    public void UnparseableVersionCountsAsZero()
    {
        Assert.False(SemVer.Satisfies("not a version", "0.1"));
        Assert.True(SemVer.Satisfies("not a version", "0.0"));
        Assert.False(SemVer.Satisfies(null, "0.1"));
    }
}
