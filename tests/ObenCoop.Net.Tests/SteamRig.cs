using System;
using System.Collections.Generic;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Lobby.Matchmaking;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport.P2P;

namespace ObenCoop.Net.Tests;

/// <summary>A player on the fake Steam: session on the peer-to-peer transport, lobby on matchmaking.</summary>
internal sealed class SteamNode
{
    public SteamNode(FakeSteam steam, string name, string gameVersion = "1.0")
    {
        Steam = steam;
        User = steam.AddUser();
        Options = new SessionOptions { PlayerName = name, GameVersion = gameVersion, Clock = () => steam.Now };
        Transport = new P2PTransport(User, Options.Clock);
        Session = new CoopSession(Transport, Options);
        Backend = new MatchmakingBackend(User, Transport, Options.Clock);
        Lobby = new CoopLobby(Session);
        Lobby.AddBackend(Backend);
        Session.PlayerJoined += p => Joined.Add(p);
        Session.PlayerLeft += (p, why) => Left.Add((p, why));
        Session.Ended += e => Ends.Add(e);
        Session.Router.Register(ChannelIds.Chat, (sender, reader) => Heard.Add(sender.Name + ": " + reader.ReadString()));
    }

    public FakeSteam Steam { get; }
    public FakeUser User { get; }
    public SessionOptions Options { get; }
    public P2PTransport Transport { get; }
    public CoopSession Session { get; }
    public MatchmakingBackend Backend { get; }
    public CoopLobby Lobby { get; }
    public List<CoopPlayer> Joined { get; } = new List<CoopPlayer>();
    public List<(CoopPlayer Player, string Reason)> Left { get; } = new List<(CoopPlayer, string)>();
    public List<SessionEnd> Ends { get; } = new List<SessionEnd>();
    public List<string> Heard { get; } = new List<string>();

    public string Id => User.LocalId.ToString();

    public void Say(string text)
    {
        var writer = MessagePool.RentWriter();
        writer.WriteString(text);
        Session.Broadcast(ChannelIds.Chat, writer);
        MessagePool.Return(writer);
    }
}

internal static class SteamRig
{
    /// <summary>Runs the fake Steam and every node in 10 ms steps until the condition holds (60 s of fake time at most).</summary>
    public static void Until(FakeSteam steam, Func<bool> condition, string what, params SteamNode[] nodes)
    {
        for (int step = 0; step < 6000; step++)
        {
            if (condition())
                return;
            Step(steam, nodes);
        }
        throw new TimeoutException("Timed out waiting for: " + what);
    }

    /// <summary>Lets <paramref name="ms"/> of fake time pass.</summary>
    public static void Run(FakeSteam steam, int ms, params SteamNode[] nodes)
    {
        for (int t = 0; t < ms; t += 10)
            Step(steam, nodes);
    }

    private static void Step(FakeSteam steam, SteamNode[] nodes)
    {
        steam.RunCallbacks();
        foreach (var node in nodes)
        {
            node.Session.Poll();
            node.Lobby.Poll();
        }
        steam.Now += 10;
    }
}
