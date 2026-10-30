using ObenCoop.Net.Lobby;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class SafeTextTests
{
    [Fact]
    public void BidiOverrideAndZeroWidthSpaceLeaveAPlayerName()
    {
        Assert.Equal("Anaevil", CoopSession.CleanName("Ana\u202Eevil\u200B"));
    }

    [Fact]
    public void BidiOverrideAndZeroWidthSpaceLeaveALobbyName()
    {
        Assert.Equal("Samflat", LobbyInfo.Clean("Sam\u202E\u200Bflat", 32));
    }

    [Fact]
    public void EmojiPairsSurvive()
    {
        Assert.Equal("Sam \uD83D\uDE00", CoopSession.CleanName("Sam \uD83D\uDE00"));
        Assert.Equal("Sam \uD83D\uDE00", LobbyInfo.Clean("Sam \uD83D\uDE00", 32));
    }

    [Fact]
    public void LoneSurrogatesAreRemoved()
    {
        Assert.Equal("AB", CoopSession.CleanName("A\uD83DB"));
        Assert.Equal("AB", CoopSession.CleanName("A\uDE00B"));
        Assert.Equal("AB", LobbyInfo.Clean("A\uD83DB", 32));
        Assert.Equal("AB", LobbyInfo.Clean("A\uDE00B", 32));
    }

    [Fact]
    public void CutNeverLeavesAHighSurrogateAtTheEnd()
    {
        // The emoji starts at index 23, so cutting at 24 would split it.
        string name = CoopSession.CleanName(new string('a', 23) + "\uD83D\uDE00");
        Assert.Equal(new string('a', 23), name);
        Assert.False(char.IsHighSurrogate(name[^1]));

        string lobby = LobbyInfo.Clean(new string('a', 31) + "\uD83D\uDE00", 32);
        Assert.Equal(new string('a', 31), lobby);
        Assert.False(char.IsHighSurrogate(lobby[^1]));
    }

    [Fact]
    public void TagsStillBecomeBrackets()
    {
        Assert.Equal("(b)", CoopSession.CleanName("<b>"));
    }

    [Theory]
    [InlineData('\u061C')]
    [InlineData('\u200B')]
    [InlineData('\u200C')]
    [InlineData('\u200D')]
    [InlineData('\u200E')]
    [InlineData('\u200F')]
    [InlineData('\u202A')]
    [InlineData('\u202E')]
    [InlineData('\u2060')]
    [InlineData('\u2066')]
    [InlineData('\u2069')]
    [InlineData('\u00AD')]
    [InlineData('\uFEFF')]
    public void HiddenCharactersAreFlagged(char c)
    {
        Assert.True(SafeText.IsHidden(c));
    }

    [Theory]
    [InlineData('A')]
    [InlineData(' ')]
    [InlineData('é')]
    public void OrdinaryCharactersAreNotFlagged(char c)
    {
        Assert.False(SafeText.IsHidden(c));
    }
}
