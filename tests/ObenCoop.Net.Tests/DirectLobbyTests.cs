using System;
using System.Linq;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using Xunit;

namespace ObenCoop.Net.Tests;

/// <summary>A node with a lobby on direct play.</summary>
internal sealed class LobbyNode : IDisposable
{
    public LobbyNode(string name, Node node = null)
    {
        Node = node ?? new Node(name);
        Direct = new DirectLobbyBackend(Node.Transport, Node.Options.Clock) { LookupTimeoutMs = 1500, ScanDurationMs = 400 };
        Lobby = new CoopLobby(Node.Session);
        Lobby.AddBackend(Direct);
        Node.Ticks.Add(Lobby.Poll);
    }

    public Node Node { get; }
    public CoopLobby Lobby { get; }
    public DirectLobbyBackend Direct { get; }
    public CoopSession Session => Node.Session;

    public void Dispose()
    {
        Lobby.Dispose();
        Direct.Dispose();
        Node.Dispose();
    }
}

public class DirectLobbyTests
{
    private static LobbyNode Host(string name = "Host")
    {
        var host = new LobbyNode(name);
        Assert.True(host.Lobby.Host(LobbyKind.Direct, "Test game", 0), host.Lobby.Error);
        return host;
    }

    private static int PortOf(LobbyNode host) => host.Node.Transport.Port;

    [Fact]
    public void HostingOpensTheLobbyAtOnce()
    {
        using var host = Host();

        Assert.Equal(LobbyStatus.Hosting, host.Lobby.Status);
        Assert.Equal(LobbyKind.Direct, host.Lobby.Kind);
        Assert.Equal(PortOf(host), host.Lobby.Info.Port);
        Assert.Equal(PortOf(host), host.Lobby.Address.Port);
        Assert.Equal("Test game", host.Lobby.Info.Name);
        Assert.True(host.Lobby.Invite().Succeeded);
    }

    [Fact]
    public void AScanFindsAGameOnThisMachine()
    {
        using var host = Host();
        using var looker = new LobbyNode("Looker");
        looker.Direct.BasePort = PortOf(host);
        looker.Direct.ScanPorts = 1;

        Assert.True(looker.Lobby.Browser.Scan(LobbyKind.Direct));
        Rig.Until(() => !looker.Lobby.Browser.Scanning, "scan over", host.Node, looker.Node);

        var found = Assert.Single(looker.Lobby.Browser.Results);
        Assert.Equal("Test game", found.Info.Name);
        Assert.Equal(LobbyVerdict.Ok, found.Verdict);
        Assert.Equal(PortOf(host), found.Address.Port);
    }

    [Fact]
    public void AHostDoesNotListItsOwnGame()
    {
        using var host = Host();
        host.Direct.BasePort = PortOf(host);
        host.Direct.ScanPorts = 1;

        host.Lobby.Browser.Scan(LobbyKind.Direct);
        Rig.Until(() => !host.Lobby.Browser.Scanning, "scan over", host.Node);

        Assert.Empty(host.Lobby.Browser.Results);
        Assert.True(host.Node.Transport.Answered > 0);
    }

    [Fact]
    public void JoiningByCodeGetsIn()
    {
        using var host = Host();
        using var client = new LobbyNode("Client");
        string code = JoinCode.ForAddress("127.0.0.1", PortOf(host));

        Assert.True(client.Lobby.Join(code), client.Lobby.Error);
        Assert.Equal(LobbyStatus.Pending, client.Lobby.Status);
        Rig.Until(() => client.Lobby.Status == LobbyStatus.Joined, "joined", host.Node, client.Node);

        Assert.Equal("Test game", client.Lobby.Info.Name);
        Assert.Equal(2, host.Session.Roster.Count);
        Assert.Equal(2, host.Lobby.Info.Players);
        Assert.True(client.Lobby.Invite().Succeeded);
    }

    [Fact]
    public void ADifferentGameVersionIsTurnedDownBeforeConnecting()
    {
        using var host = Host();
        using var client = new LobbyNode("Client");
        client.Node.Options.GameVersion = "2.0";
        int connections = 0;
        host.Node.Transport.PeerConnected += _ => connections++;

        Assert.True(client.Lobby.Join("127.0.0.1:" + PortOf(host)));
        Rig.Until(() => client.Lobby.Status == LobbyStatus.Offline, "refused", host.Node, client.Node);

        Assert.Contains("Obenseuer 1.0", client.Lobby.Error);
        Assert.Equal(0, connections);
        Assert.Equal(SessionState.Offline, client.Session.State);
    }

    [Fact]
    public void TheHostLeavingTakesTheClientOut()
    {
        using var host = Host();
        using var client = new LobbyNode("Client");
        client.Lobby.Join("127.0.0.1:" + PortOf(host));
        Rig.Until(() => client.Lobby.Status == LobbyStatus.Joined, "joined", host.Node, client.Node);

        host.Lobby.Leave();
        Rig.Until(() => client.Lobby.Status == LobbyStatus.Offline, "client out", host.Node, client.Node);

        Assert.Equal(LobbyStatus.Offline, host.Lobby.Status);
        Assert.Equal("", host.Lobby.Error);
        Assert.NotEqual("", client.Lobby.Error);
        Assert.Equal(LobbyKind.None, client.Lobby.Kind);
    }

    [Fact]
    public void NobodyAnsweringEndsTheRequest()
    {
        var probe = new DirectTransport();
        probe.Host(0, 1);
        int deadPort = probe.Port;
        probe.Stop();
        using var client = new LobbyNode("Client");
        client.Lobby.Options.RequestTimeoutMs = 2500;

        Assert.True(client.Lobby.Join("127.0.0.1:" + deadPort));
        Rig.Until(() => client.Lobby.Status == LobbyStatus.Offline, "gave up", client.Node);
        Assert.NotEqual("", client.Lobby.Error);
    }

    [Fact]
    public void RequestsAreRefusedWhileOneRunsOrWhileHosting()
    {
        using var host = Host();
        Assert.False(host.Lobby.Join("127.0.0.1:1"));
        Assert.Contains("hosting", host.Lobby.Error);
        Assert.False(host.Lobby.Host(LobbyKind.Direct, "again", 0));
        Assert.False(host.Lobby.Host(LobbyKind.Steam, "steam", 0));
        Assert.Contains("Steam", host.Lobby.Error);
        using var other = new LobbyNode("Other");
        Assert.False(other.Lobby.Join("   "));
        Assert.False(other.Lobby.Join("AAAA-AAAA-AAAA"));
        Assert.Contains("typo", other.Lobby.Error);
    }

    [Fact]
    public void AnOpenLobbyCarriesOverAReload()
    {
        using var host = Host();
        using var client = new LobbyNode("Client");
        client.Lobby.Join("127.0.0.1:" + PortOf(host));
        Rig.Until(() => client.Lobby.Status == LobbyStatus.Joined, "joined", host.Node, client.Node);

        // What the mod does on unload and in the next copy's OnLoad.
        byte[] session = host.Session.SaveState();
        byte[] lobby = host.Lobby.SaveState();
        object[] connection = host.Node.Transport.Detach();
        host.Lobby.Dispose();

        var reloaded = new Node("Host");
        reloaded.Transport.Adopt(connection);
        Assert.True(reloaded.Session.RestoreState(session));
        using var next = new LobbyNode("Host", reloaded);
        Assert.True(next.Lobby.RestoreState(lobby));
        Assert.Equal(LobbyStatus.Hosting, next.Lobby.Status);

        using var looker = new LobbyNode("Looker");
        looker.Direct.BasePort = reloaded.Transport.Port;
        looker.Direct.ScanPorts = 1;
        looker.Lobby.Browser.Scan(LobbyKind.Direct);
        Rig.Until(() => !looker.Lobby.Browser.Scanning, "scan over", next.Node, client.Node, looker.Node);
        Assert.Equal(2, looker.Lobby.Browser.Results.Single().Info.Players);
    }
}
