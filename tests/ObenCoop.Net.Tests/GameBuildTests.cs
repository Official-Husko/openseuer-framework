using ObenCoop.Net.Lobby;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class GameBuildTests
{
    private static SessionEnd JoinExpectingEnd(Node host, Node joiner)
    {
        joiner.Session.Join(Rig.Address(host));
        Rig.Until(() => joiner.Ends.Count > 0 || joiner.Session.State == SessionState.Joined, "joiner is answered", host, joiner);
        return joiner.Ends.Count > 0 ? joiner.Ends[0] : null;
    }

    [Fact]
    public void ADifferentBuildOfTheSameVersionIsRejected()
    {
        using var host = Rig.StartHost();
        host.Options.GameBuild = "72c44ab48d2ea50f";
        using var client = new Node("C");
        client.Options.GameBuild = "0000aaaabbbbcccc";

        var end = JoinExpectingEnd(host, client);

        Assert.NotNull(end);
        Assert.Equal(EndKind.Rejected, end.Kind);
        Assert.Equal(RejectReason.GameBuild, end.Reject);
        Assert.Contains("1.0", end.Message);
        Assert.Equal(1, host.Session.Roster.Count);
    }

    [Theory]
    [InlineData("72c44ab48d2ea50f", "72c44ab48d2ea50f")]
    [InlineData("72c44ab48d2ea50f", "")]
    [InlineData("", "72c44ab48d2ea50f")]
    public void TheSameBuildOrAnUnknownOneJoins(string hostBuild, string clientBuild)
    {
        using var host = Rig.StartHost();
        host.Options.GameBuild = hostBuild;
        using var client = new Node("C");
        client.Options.GameBuild = clientBuild;

        Assert.Null(JoinExpectingEnd(host, client));
        Assert.Equal(2, host.Session.Roster.Count);
    }

    [Fact]
    public void ALobbyListShowsTheOtherBuildBeforeJoining()
    {
        var local = new SessionOptions { GameVersion = "0.5.13", GameBuild = "aaaa" };
        var lobby = new LobbyInfo { GameVersion = "0.5.13", GameBuild = "bbbb", ModsFingerprint = ModSet.Empty.Fingerprint, MaxPlayers = 4, Players = 1 };
        Assert.True(LobbyInfo.TryFromBytes(lobby.ToBytes(), out var read));
        Assert.Equal("bbbb", read.GameBuild);

        Assert.Equal(LobbyVerdict.GameBuild, LobbyCheck.Check(read, local, out string message));
        Assert.Contains("build", message);
        Assert.False(LobbyCheck.CanTry(LobbyVerdict.GameBuild));

        local.GameBuild = "";
        Assert.Equal(LobbyVerdict.Ok, LobbyCheck.Check(read, local, out _));
    }
}
