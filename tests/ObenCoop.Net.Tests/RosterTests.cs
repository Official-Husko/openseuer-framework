using System.Linq;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class RosterTests
{
    [Fact]
    public void ClientLeavingUpdatesHostAndTheOtherClient()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        Rig.Until(() => a.Session.Roster.Count == 3, "roster settled", host, a, b);

        b.Session.Leave();
        Rig.Until(() => host.Session.Roster.Count == 2 && a.Session.Roster.Count == 2, "roster shrinks", host, a, b);

        Assert.Equal("B", host.Left.Single().Player.Name);
        Assert.Equal("B", a.Left.Single().Player.Name);
        Assert.Equal(EndKind.Left, b.Ends.Single().Kind);
        Assert.Equal(SessionState.Offline, b.Session.State);
        Assert.Equal(0, b.Session.Roster.Count);
    }

    [Fact]
    public void FreedSlotIsReusedByTheNextJoiner()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        a.Session.Leave();
        Rig.Until(() => host.Session.Roster.Count == 2, "A gone", host, b);

        using var c = Rig.JoinedClient(host, "C", b);

        Assert.Equal(1, c.Session.Roster.Local.Slot);
        Assert.Equal(new byte[] { 0, 1, 2 }, host.Session.Roster.Players.Select(p => p.Slot).ToArray());
    }

    [Fact]
    public void HostStoppingEndsTheClientsSession()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");

        host.Session.Leave();
        Rig.Until(() => client.Ends.Count == 1, "client notices", host, client);

        Assert.Equal(EndKind.ConnectionLost, client.Ends[0].Kind);
        Assert.Contains("host", client.Ends[0].Message);
        Assert.Equal(SessionState.Offline, client.Session.State);
        Assert.Equal(0, client.Session.Roster.Count);
        Assert.Equal(EndKind.Left, host.Ends.Single().Kind);
    }

    [Fact]
    public void ClientVanishingWithoutLeavingIsNoticedByTheHost()
    {
        using var host = Rig.StartHost();
        var client = Rig.JoinedClient(host, "C");
        Rig.Until(() => host.Session.Roster.Count == 2, "client counted", host, client);

        // Closing the socket without the session's goodbye is what a crashed game looks like to the host.
        client.Transport.Stop();
        Rig.Until(() => host.Session.Roster.Count == 1, "host drops the client", host);

        Assert.Single(host.Left);
    }

    [Fact]
    public void LeavingAndJoiningAgainWorksWithTheSameSessionObjects()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        client.Session.Leave();
        Rig.Until(() => host.Session.Roster.Count == 1, "client gone", host);

        client.Session.Join(Rig.Address(host));
        Rig.Until(() => client.Session.State == SessionState.Joined && host.Session.Roster.Count == 2, "rejoined", host, client);

        Assert.Equal(2, client.StartedCount);
    }

    [Fact]
    public void HostCanStopAndHostAgain()
    {
        using var host = Rig.StartHost();
        host.Session.Leave();
        host.Session.Host(0);

        Assert.Equal(SessionState.Hosting, host.Session.State);
        Assert.Equal(1, host.Session.Roster.Count);
    }
}
