using ObenUIKit;

namespace ObenCore.Tests.Kit;

// The cases the reference framework's KeyChordSelfTest checked at start, plus the ranking between chords.
public class ChordRulesTests
{
    [Fact]
    public void APlainKeyMatchesWhenItGoesDown() => Assert.True(ChordRules.Matches(true, KeyModifiers.None, KeyModifiers.None));

    [Fact]
    public void ExtraHeldModifiersDontStopAChord()
    {
        Assert.True(ChordRules.Matches(true, KeyModifiers.None, KeyModifiers.Ctrl));
        Assert.True(ChordRules.Matches(true, KeyModifiers.Ctrl, KeyModifiers.Ctrl | KeyModifiers.Alt));
    }

    [Fact]
    public void AChordNeedsEveryModifier()
    {
        Assert.True(ChordRules.Matches(true, KeyModifiers.Ctrl | KeyModifiers.Shift, KeyModifiers.Ctrl | KeyModifiers.Shift));
        Assert.False(ChordRules.Matches(true, KeyModifiers.Ctrl | KeyModifiers.Shift, KeyModifiers.Ctrl));
        Assert.False(ChordRules.Matches(true, KeyModifiers.Ctrl, KeyModifiers.None));
    }

    [Fact]
    public void NothingMatchesWithoutTheMainKey()
    {
        Assert.False(ChordRules.Matches(false, KeyModifiers.None, KeyModifiers.None));
        Assert.False(ChordRules.Matches(false, KeyModifiers.Ctrl, KeyModifiers.Ctrl));
    }

    [Fact]
    public void TheMoreSpecificChordWinsWhileItsModifiersAreHeld()
    {
        Assert.True(ChordRules.Outranked(KeyModifiers.None, KeyModifiers.Ctrl, KeyModifiers.Ctrl));
        Assert.True(ChordRules.Outranked(KeyModifiers.Ctrl, KeyModifiers.Ctrl | KeyModifiers.Shift, KeyModifiers.Ctrl | KeyModifiers.Shift));
        Assert.False(ChordRules.Outranked(KeyModifiers.None, KeyModifiers.Ctrl, KeyModifiers.None));
    }

    [Fact]
    public void ChordsThatDontContainEachOtherDontRank()
    {
        Assert.False(ChordRules.Outranked(KeyModifiers.Ctrl, KeyModifiers.Shift, KeyModifiers.Ctrl | KeyModifiers.Shift));
        Assert.False(ChordRules.Outranked(KeyModifiers.Ctrl, KeyModifiers.Ctrl, KeyModifiers.Ctrl));
        Assert.False(ChordRules.Outranked(KeyModifiers.Ctrl | KeyModifiers.Shift, KeyModifiers.Ctrl, KeyModifiers.Ctrl | KeyModifiers.Shift));
    }

    [Theory]
    [InlineData(KeyModifiers.None, KeyModifiers.Ctrl, true)]
    [InlineData(KeyModifiers.Ctrl, KeyModifiers.Ctrl | KeyModifiers.Shift, true)]
    [InlineData(KeyModifiers.Alt, KeyModifiers.Alt, true)]
    [InlineData(KeyModifiers.Ctrl, KeyModifiers.Shift, false)]
    [InlineData(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyModifiers.Ctrl | KeyModifiers.Shift, false)]
    public void OverlapMeansOneContainsTheOther(KeyModifiers a, KeyModifiers b, bool expected)
    {
        Assert.Equal(expected, ChordRules.Overlap(a, b));
        Assert.Equal(expected, ChordRules.Overlap(b, a));
    }
}
