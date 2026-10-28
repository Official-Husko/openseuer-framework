using System.Collections.Generic;
using System.Linq;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Lobby.Matchmaking;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

/// <summary>Steam lobbies end to end on the fake Steam: create, list, check, join, invites, leaving.</summary>
public class SteamLobbyTests
{
    private static SteamNode Hosting(FakeSteam steam, string name = "Host")
    {
        var host = new SteamNode(steam, name);
        Assert.True(host.Lobby.Host(LobbyKind.Steam, "Steam game", 0), host.Lobby.Error);
        Assert.Equal(LobbyStatus.Pending, host.Lobby.Status);
        SteamRig.Until(steam, () => host.Lobby.Status == LobbyStatus.Hosting, "lobby created", host);
        return host;
    }

    private static FakeLobby RoomOf(FakeSteam steam, SteamNode host) => steam.Lobbies[host.Lobby.Address.LobbyId];

    [Fact]
    public void HostingCreatesALobbyThatSaysWhatItIs()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var room = RoomOf(steam, host);

        Assert.Equal(MatchmakingBackend.GameTag, room.Data[MatchmakingBackend.GameKey]);
        Assert.True(LobbyInfo.TryFromBase64(room.Data[MatchmakingBackend.InfoKey], out var info));
        Assert.Equal("Steam game", info.Name);
        Assert.Equal(LobbyVisibility.FriendsOnly, room.Visibility);
        Assert.Equal(room.Id, host.User.JoinTarget);

        var invite = host.Lobby.Invite();
        Assert.True(invite.OverlayOpened);
        Assert.Equal(1, host.User.InviteDialogs);
        Assert.True(JoinCode.TryDecode(invite.Code, out var coded));
        Assert.Equal(room.Id, coded.LobbyId);
    }

    [Fact]
    public void AScanListsFriendsLobbies()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var looker = new SteamNode(steam, "Looker");

        Assert.True(looker.Lobby.Browser.Scan(LobbyKind.Steam));
        SteamRig.Until(steam, () => !looker.Lobby.Browser.Scanning, "scan over", host, looker);

        var found = Assert.Single(looker.Lobby.Browser.Results);
        Assert.Equal("Steam game", found.Info.Name);
        Assert.Equal(LobbyVerdict.Ok, found.Verdict);
        Assert.Equal(LobbyKind.Steam, found.Address.Kind);
    }

    [Fact]
    public void JoiningByCodeConnectsOverPeerToPeer()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var client = new SteamNode(steam, "Client");
        string code = JoinCode.ForSteamLobby(host.Lobby.Address.LobbyId);

        Assert.True(client.Lobby.Join(code), client.Lobby.Error);
        SteamRig.Until(steam, () => client.Lobby.Status == LobbyStatus.Joined, "joined", host, client);

        Assert.Equal(2, host.Session.Roster.Count);
        Assert.Equal(2, RoomOf(steam, host).Members.Count);
        Assert.True(LobbyInfo.TryFromBase64(RoomOf(steam, host).Data[MatchmakingBackend.InfoKey], out var info));
        Assert.Equal(2, info.Players);
        Assert.Equal(host.Lobby.Address.LobbyId, client.User.JoinTarget);

        client.Say("over steam");
        SteamRig.Until(steam, () => host.Heard.Count == 1, "chat", host, client);
    }

    [Fact]
    public void ADifferentGameVersionLeavesTheLobbyAgain()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var client = new SteamNode(steam, "Client", gameVersion: "2.0");

        client.Lobby.Join(LobbyAddress.Steam(host.Lobby.Address.LobbyId));
        SteamRig.Until(steam, () => client.Lobby.Status == LobbyStatus.Offline, "refused", host, client);

        Assert.Contains("Obenseuer 1.0", client.Lobby.Error);
        Assert.Single(RoomOf(steam, host).Members);
        Assert.Equal(1, host.Session.Roster.Count);
    }

    [Fact]
    public void ALobbyOfAnotherGameIsLeftAgain()
    {
        var steam = new FakeSteam();
        var stranger = steam.AddUser();
        ulong lobby = 0;
        stranger.CreateLobby(LobbyVisibility.Public, 4, id => lobby = id);
        steam.RunCallbacks();
        stranger.SetData(lobby, MatchmakingBackend.GameKey, "another-game");
        var client = new SteamNode(steam, "Client");

        client.Lobby.Join(LobbyAddress.Steam(lobby));
        SteamRig.Until(steam, () => client.Lobby.Status == LobbyStatus.Offline, "refused", client);

        Assert.Contains("isn't an ObenCoop game", client.Lobby.Error);
        Assert.Single(steam.Lobbies[lobby].Members);
    }

    [Fact]
    public void AnAcceptedInviteIsPassedOn()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var client = new SteamNode(steam, "Client");
        var requested = new List<LobbyAddress>();
        client.Lobby.JoinRequested += requested.Add;

        client.User.Invited(host.Lobby.Address.LobbyId);

        Assert.Equal(host.Lobby.Address, requested.Single());
    }

    [Fact]
    public void OnlyLobbyMembersGetIn()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var outsider = new SteamNode(steam, "Outsider");

        // Straight to the host over peer to peer, without entering the lobby.
        outsider.Session.Join(host.Id);
        SteamRig.Until(steam, () => outsider.Ends.Count == 1, "turned away", host, outsider);

        Assert.Equal(EndKind.ConnectFailed, outsider.Ends[0].Kind);
        Assert.Equal(1, host.Session.Roster.Count);
    }

    [Fact]
    public void LeavingTheSteamLobbyDropsThePlayer()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var client = new SteamNode(steam, "Client");
        client.Lobby.Join(LobbyAddress.Steam(host.Lobby.Address.LobbyId));
        SteamRig.Until(steam, () => client.Lobby.Status == LobbyStatus.Joined, "joined", host, client);

        // Steam says they left (the overlay, a crash) before any goodbye arrived.
        client.User.Offline = true;
        client.User.LeaveLobby(host.Lobby.Address.LobbyId);
        SteamRig.Until(steam, () => host.Left.Count == 1, "dropped", host, client);
        Assert.Equal("Left the lobby.", host.Left[0].Reason);
    }

    [Fact]
    public void TheHostLeavingEndsItForEveryone()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        var client = new SteamNode(steam, "Client");
        client.Lobby.Join(LobbyAddress.Steam(host.Lobby.Address.LobbyId));
        SteamRig.Until(steam, () => client.Lobby.Status == LobbyStatus.Joined, "joined", host, client);
        ulong lobby = host.Lobby.Address.LobbyId;

        host.Lobby.Leave();
        SteamRig.Until(steam, () => client.Lobby.Status == LobbyStatus.Offline, "client out", host, client);

        Assert.False(steam.Lobbies.ContainsKey(lobby));
        Assert.Equal(0UL, host.User.JoinTarget);
        Assert.NotEqual("", client.Lobby.Error);
    }

    [Fact]
    public void GivingUpWhileTheLobbyIsMadeLeavesIt()
    {
        var steam = new FakeSteam();
        var host = new SteamNode(steam, "Host");
        host.Lobby.Host(LobbyKind.Steam, "x", 0);
        host.Lobby.Leave();
        SteamRig.Run(steam, 100, host);

        Assert.Empty(steam.Lobbies);
        Assert.Equal(LobbyStatus.Offline, host.Lobby.Status);
    }

    [Fact]
    public void AnOpenSteamLobbyCarriesOverAReload()
    {
        var steam = new FakeSteam();
        var host = Hosting(steam);
        byte[] lobbyState = host.Lobby.SaveState();
        byte[] sessionState = host.Session.SaveState();
        object[] connection = host.Transport.Detach();
        host.Lobby.Dispose();

        var transport = new ObenCoop.Net.Transport.P2P.P2PTransport(host.User, () => steam.Now);
        var session = new CoopSession(transport, host.Options);
        var lobby = new CoopLobby(session);
        lobby.AddBackend(new MatchmakingBackend(host.User, transport, () => steam.Now));
        Assert.True(transport.Adopt(connection));
        Assert.True(session.RestoreState(sessionState));
        Assert.True(lobby.RestoreState(lobbyState));

        Assert.Equal(LobbyStatus.Hosting, lobby.Status);
        Assert.Equal(LobbyKind.Steam, lobby.Kind);
        Assert.True(lobby.Invite().OverlayOpened);
    }
}
