using ObenCoop.Net.Lobby;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class LobbyInfoTests
{
    private static readonly ModSet Mods = new ModSet(new[] { new ModEntry("ObenCoop", "0.1.0"), new ModEntry("ObenFramework", "0.1.0") });

    private static SessionOptions Local() => new SessionOptions { GameVersion = "0.5.13", Mods = Mods };

    private static LobbyInfo Matching() => new LobbyInfo
    {
        Id = 77,
        GameVersion = "0.5.13",
        ModsFingerprint = Mods.Fingerprint,
        ModCount = 2,
        Name = "Sam's flat",
        HostName = "Sam",
        Players = 1,
        MaxPlayers = 4,
        Port = 27890,
    };

    [Fact]
    public void RoundTripsThroughBytesAndBase64()
    {
        var info = Matching();
        Assert.True(LobbyInfo.TryFromBytes(info.ToBytes(), out var back));
        Assert.True(LobbyInfo.TryFromBase64(info.ToBase64(), out var text));

        foreach (var got in new[] { back, text })
        {
            Assert.Equal(info.Id, got.Id);
            Assert.Equal(Protocol.Version, got.Protocol);
            Assert.Equal("0.5.13", got.GameVersion);
            Assert.Equal(info.ModsFingerprint, got.ModsFingerprint);
            Assert.Equal("Sam's flat", got.Name);
            Assert.Equal("Sam", got.HostName);
            Assert.Equal(1, got.Players);
            Assert.Equal(4, got.MaxPlayers);
            Assert.Equal(27890, got.Port);
            Assert.True(got.Open);
        }
    }

    [Fact]
    public void DamagedBytesAreNotRead()
    {
        var bytes = Matching().ToBytes();
        Assert.False(LobbyInfo.TryFromBytes(new[] { bytes[0], bytes[1] }, out _));
        Assert.False(LobbyInfo.TryFromBase64("not base64!", out _));
    }

    [Fact]
    public void NamesFromOtherPlayersLoseTheirTags()
    {
        var info = Matching();
        info.Name = "<color=red>Party</color>\n";
        Assert.True(LobbyInfo.TryFromBytes(info.ToBytes(), out var got));
        Assert.DoesNotContain("<", got.Name);
        Assert.DoesNotContain("\n", got.Name);
    }

    [Fact]
    public void AMatchingLobbyPasses()
    {
        Assert.Equal(LobbyVerdict.Ok, LobbyCheck.Check(Matching(), Local(), out string message));
        Assert.Null(message);
    }

    [Fact]
    public void ChecksComeInTheHandshakeOrder()
    {
        var info = Matching();
        info.Protocol = Protocol.Version + 1;
        info.GameVersion = "0.6.0";
        Assert.Equal(LobbyVerdict.ProtocolVersion, LobbyCheck.Check(info, Local(), out _));

        info.Protocol = Protocol.Version;
        Assert.Equal(LobbyVerdict.GameVersion, LobbyCheck.Check(info, Local(), out string versionText));
        Assert.Contains("0.6.0", versionText);

        info = Matching();
        info.Players = 4;
        Assert.Equal(LobbyVerdict.Full, LobbyCheck.Check(info, Local(), out _));

        info = Matching();
        info.Open = false;
        Assert.Equal(LobbyVerdict.Closed, LobbyCheck.Check(info, Local(), out _));
    }

    [Fact]
    public void DifferentModsStillLetTheJoinGoAheadSoTheHostCanSayWhich()
    {
        var info = Matching();
        info.ModsFingerprint ^= 1;
        var verdict = LobbyCheck.Check(info, Local(), out _);

        Assert.Equal(LobbyVerdict.ModSet, verdict);
        Assert.True(LobbyCheck.CanTry(verdict));
        Assert.False(LobbyCheck.CanTry(LobbyVerdict.GameVersion));
    }

    [Fact]
    public void ANewerFormatReadsAsADifferentProtocol()
    {
        var writer = new MessageWriter();
        writer.WriteByte(9);
        writer.WriteInt32(Protocol.Version + 3);
        writer.WriteString("whatever comes next");
        var reader = new MessageReader().Init(writer.AsSegment());

        Assert.True(LobbyInfo.TryRead(reader, out var info));
        Assert.True(info.Unreadable);
        Assert.Equal(LobbyVerdict.ProtocolVersion, LobbyCheck.Check(info, Local(), out _));
    }

    [Fact]
    public void FingerprintsFollowTheModsNotTheirOrder()
    {
        var reversed = new ModSet(new[] { new ModEntry("ObenFramework", "0.1.0"), new ModEntry("ObenCoop", "0.1.0") });
        var newer = new ModSet(new[] { new ModEntry("ObenCoop", "0.2.0"), new ModEntry("ObenFramework", "0.1.0") });
        var shifted = new ModSet(new[] { new ModEntry("ObenCoo", "p0.1.0"), new ModEntry("ObenFramework", "0.1.0") });

        Assert.Equal(Mods.Fingerprint, reversed.Fingerprint);
        Assert.NotEqual(Mods.Fingerprint, newer.Fingerprint);
        Assert.NotEqual(Mods.Fingerprint, shifted.Fingerprint);
        Assert.NotEqual(Mods.Fingerprint, ModSet.Empty.Fingerprint);
    }

    [Fact]
    public void DiscoveryPacketsRoundTrip()
    {
        var query = LobbyWire.Query;
        Assert.True(LobbyWire.IsQuery(query, 0, query.Length));
        Assert.False(LobbyWire.IsQuery(new byte[] { 1, 2, 3, 4, 5 }, 0, 5));

        var reply = LobbyWire.Reply(Matching());
        Assert.False(LobbyWire.IsQuery(reply, 0, reply.Length));
        Assert.True(LobbyWire.TryReadReply(reply, 0, reply.Length, out var info));
        Assert.Equal("Sam's flat", info.Name);
        Assert.False(LobbyWire.TryReadReply(query, 0, query.Length, out _));
    }
}
