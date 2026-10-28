using System;
using System.Linq;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class HandshakeTests
{
    [Fact]
    public void HostStartsInSlotZeroWithItselfOnTheRoster()
    {
        using var host = Rig.StartHost();

        Assert.Equal(SessionState.Hosting, host.Session.State);
        Assert.True(host.Session.IsHost);
        Assert.Equal(1, host.StartedCount);
        Assert.Single(host.Session.Roster.Players);
        Assert.Equal(CoopPlayer.HostSlot, host.Session.Roster.Local.Slot);
        Assert.Same(host.Session.Roster.Local, host.Session.Roster.Host);
    }

    [Fact]
    public void ClientJoinsAndBothSidesAgreeOnTheRoster()
    {
        using var host = Rig.StartHost("Hosty");
        using var client = Rig.JoinedClient(host, "Joiny");
        Rig.Until(() => host.Session.Roster.Count == 2, "host sees the client", host, client);

        Assert.Equal(1, client.StartedCount);
        Assert.Equal(1, client.Session.Roster.Local.Slot);
        Assert.Equal("Hosty", client.Session.Roster.Host.Name);
        Assert.Equal(new[] { "Hosty", "Joiny" }, host.Session.Roster.Players.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "Hosty", "Joiny" }, client.Session.Roster.Players.Select(p => p.Name).ToArray());

        Assert.Equal("Joiny", host.Joined.Single().Name);
        Assert.Equal("Hosty", client.Joined.Single().Name);
        Assert.False(client.Session.Roster.Host.IsLocal);
    }

    [Fact]
    public void PlayerIdsAreTheOnesFromTheOptions()
    {
        using var host = Rig.StartHost();
        var id = Guid.NewGuid();
        var client = new Node("Joiny");
        client.Options.PlayerId = id;
        using (client)
        {
            client.Session.Join(Rig.Address(host));
            Rig.Until(() => host.Session.Roster.Count == 2 && client.Session.State == SessionState.Joined, "client in roster", host, client);
            Assert.Equal(id, host.Session.Roster.Find(1).Id);
            Assert.Equal(host.Options.PlayerId, client.Session.Roster.Host.Id);
        }
    }

    [Fact]
    public void LaterJoinersGetTheNextSlotAndEarlierClientsHearAboutThem()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        Rig.Until(() => a.Session.Roster.Count == 3, "A learns about B", host, a, b);

        Assert.Equal(2, b.Session.Roster.Local.Slot);
        Assert.Contains(a.Joined, p => p.Name == "B" && p.Slot == 2);
        Assert.Equal(3, b.Session.Roster.Count);
    }

    [Fact]
    public void SameNameGetsANumber()
    {
        using var host = Rig.StartHost("Sam");
        using var client = Rig.JoinedClient(host, "Sam");
        Rig.Until(() => host.Session.Roster.Count == 2, "client in roster", host, client);

        Assert.Equal("Sam 2", host.Session.Roster.Find(1).Name);
        Assert.Equal("Sam 2", client.Session.Roster.Local.Name);
    }

    [Fact]
    public void ModSetsThatMatchAreAccepted()
    {
        var mods = new ModSet(new[] { new ModEntry("ObenCoop", "0.1.0"), new ModEntry("ObenFramework", "0.1.0") });
        var options = new SessionOptions { Mods = mods };
        using var host = Rig.StartHost(options: options);
        var client = new Node("C");
        client.Options.Mods = new ModSet(new[] { new ModEntry("ObenFramework", "0.1.0"), new ModEntry("ObenCoop", "0.1.0") });
        using (client)
        {
            client.Session.Join(Rig.Address(host));
            Rig.Until(() => client.Session.State == SessionState.Joined, "joined", host, client);
        }
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3", true)]
    [InlineData("host:9", "host", true)]
    [InlineData("[::1]:80", "[::1]", true)]
    [InlineData("host:", null, false)]
    [InlineData(":9", null, false)]
    [InlineData("host:99999", null, false)]
    [InlineData("", null, false)]
    public void AddressParsing(string text, string host, bool ok)
    {
        bool parsed = CoopSession.TryParseAddress(text, 5, out string h, out int port);
        Assert.Equal(ok, parsed);
        if (ok)
            Assert.Equal(host, h);
    }
}
