using System.Linq;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

/// <summary>The session over the peer-to-peer transport, without lobbies (the transport lets everyone in).</summary>
public class P2PTransportTests
{
    private static (FakeSteam, SteamNode, SteamNode) Pair()
    {
        var steam = new FakeSteam();
        var host = new SteamNode(steam, "Host");
        var client = new SteamNode(steam, "Client");
        host.Session.Host(0);
        client.Session.Join(host.Id);
        SteamRig.Until(steam, () => client.Session.State == SessionState.Joined, "joined", host, client);
        return (steam, host, client);
    }

    [Fact]
    public void TheSessionHandshakeAndMessagesWorkOverIt()
    {
        var (steam, host, client) = Pair();

        client.Say("hello");
        host.Say("hi");
        SteamRig.Until(steam, () => host.Heard.Count == 1 && client.Heard.Count == 1, "chat", host, client);

        Assert.Equal("Client: hello", host.Heard[0]);
        Assert.Equal("Host: hi", client.Heard[0]);
        Assert.Equal(2, host.Session.Roster.Count);
        Assert.True(host.Transport.Stats.PacketsReceived > 0);
    }

    [Fact]
    public void ARelayedMessageReachesAThirdPlayer()
    {
        var (steam, host, a) = Pair();
        var b = new SteamNode(steam, "B");
        b.Session.Join(host.Id);
        SteamRig.Until(steam, () => b.Session.State == SessionState.Joined && a.Session.Roster.Count == 3, "B joined", host, a, b);

        a.Say("to everyone");
        SteamRig.Until(steam, () => b.Heard.Count == 1 && host.Heard.Count == 1, "relayed", host, a, b);
        Assert.Equal("Client: to everyone", b.Heard[0]);
    }

    [Fact]
    public void LeavingSaysGoodbyeAndClosesTheSessionAfterAWhile()
    {
        var (steam, host, client) = Pair();

        client.Session.Leave();
        SteamRig.Until(steam, () => host.Left.Count == 1, "host notices", host, client);
        Assert.Equal("The other side closed the game or left.", host.Left[0].Reason);

        SteamRig.Run(steam, 2500, host, client);
        Assert.Contains(client.User.LocalId, host.User.Closed);
        Assert.Contains(host.User.LocalId, client.User.Closed);
    }

    [Fact]
    public void AHostRefusalReachesTheJoinerWithItsReason()
    {
        var steam = new FakeSteam();
        var host = new SteamNode(steam, "Host");
        var client = new SteamNode(steam, "Client", gameVersion: "9.9");
        host.Session.Host(0);
        client.Session.Join(host.Id);
        SteamRig.Until(steam, () => client.Ends.Count == 1, "refused", host, client);

        Assert.Equal(EndKind.Rejected, client.Ends[0].Kind);
        Assert.Equal(RejectReason.GameVersion, client.Ends[0].Reject);
    }

    [Fact]
    public void ASilentPeerTimesOut()
    {
        var (steam, host, client) = Pair();

        client.User.Offline = true;
        SteamRig.Until(steam, () => host.Left.Count == 1, "dropped", host, client);
        Assert.Equal("The connection timed out.", host.Left[0].Reason);
        Assert.True(steam.Now > 20000);
    }

    [Fact]
    public void ANetworkFailureEndsTheConnection()
    {
        var (steam, host, client) = Pair();

        client.User.Fail(host.User.LocalId, "Steam lost the connection.");
        SteamRig.Until(steam, () => client.Ends.Count == 1, "ended", host, client);
        Assert.Equal(EndKind.ConnectionLost, client.Ends[0].Kind);
        Assert.Contains("Steam lost", client.Ends[0].Message);
    }

    [Fact]
    public void NobodyAnsweringFailsTheConnect()
    {
        var steam = new FakeSteam();
        var client = new SteamNode(steam, "Client");
        var ghost = steam.AddUser();
        ghost.Offline = true;

        client.Session.Join(ghost.LocalId.ToString());
        SteamRig.Until(steam, () => client.Ends.Count == 1, "gave up", client);
        Assert.Equal(EndKind.ConnectFailed, client.Ends[0].Kind);
    }

    [Fact]
    public void PingsMeasureTheRoundTrip()
    {
        var (steam, host, client) = Pair();
        SteamRig.Run(steam, 1500, host, client);

        Assert.True(host.Session.PingOf(host.Session.Roster.Players.Single(p => !p.IsLocal)) >= 0);
        Assert.True(client.Session.PingOf(client.Session.Roster.Host) >= 0);
    }

    [Fact]
    public void ConnectionsCarryOverAHandover()
    {
        var (steam, host, client) = Pair();

        byte[] state = host.Session.SaveState();
        object[] parked = host.Transport.Detach();
        Assert.Null(host.Transport.Detach());
        // The next copy runs on the same Steam user.
        var transport = new ObenCoop.Net.Transport.P2P.P2PTransport(host.User, () => steam.Now);
        var session = new CoopSession(transport, host.Options);
        string heard = null;
        session.Router.Register(Messaging.ChannelIds.Chat, (sender, reader) => heard = reader.ReadString());
        Assert.True(transport.Adopt(parked));
        Assert.True(session.RestoreState(state));

        client.Say("still there?");
        for (int i = 0; i < 50 && heard == null; i++)
        {
            steam.RunCallbacks();
            session.Poll();
            client.Session.Poll();
            steam.Now += 10;
        }
        Assert.Equal("still there?", heard);
        Assert.Equal(2, session.Roster.Count);
    }
}
