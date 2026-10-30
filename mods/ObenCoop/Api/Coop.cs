using System;
using System.Collections.Generic;
using ObenCoop.Net.Blobs;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Presence;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using ObenFramework.Events;
using ObenLoader.Runtime;

namespace ObenCoop;

/// <summary>
/// Co-op for other mods: who is playing, messages and big payloads on channels named by the mod, where
/// the other players are, and the lobby. Add <c>"ObenCoop"</c> to the mod's dependencies in mod.json and
/// reference ObenCoop.dll and ObenCoop.Net.dll. Everything runs on the main thread, from ObenCoop's
/// update. Subscriptions and listeners take the mod as owner and go away when it unloads; when ObenCoop
/// itself reloads, the mods that depend on it reload with it and subscribe again.
/// </summary>
public static class Coop
{
    private static readonly IReadOnlyList<CoopPlayer> Nobody = Array.Empty<CoopPlayer>();
    private static readonly HandlerList<Action> StartedHandlers = new HandlerList<Action>();
    private static readonly HandlerList<Action<CoopPlayer>> JoinedHandlers = new HandlerList<Action<CoopPlayer>>();
    private static readonly HandlerList<Action<CoopPlayer, string>> LeftHandlers = new HandlerList<Action<CoopPlayer, string>>();
    private static readonly HandlerList<Action<SessionEnd>> EndedHandlers = new HandlerList<Action<SessionEnd>>();

    private static CoopMod _mod;

    /// <summary>ObenCoop is loaded and running (false only while it starts or after it was switched off).</summary>
    public static bool IsAvailable => _mod != null;

    /// <summary>In a co-op game: hosting one, or joined to a host.</summary>
    public static bool InGame => _mod != null && _mod.Session.IsActive;

    /// <summary>This game hosts the co-op game; the host is the authority over the shared world.</summary>
    public static bool IsHost => _mod != null && _mod.Session.IsHost;

    /// <summary>This player in the roster, null when not in a game.</summary>
    public static CoopPlayer Me => InGame ? _mod.Session.Roster.Local : null;

    /// <summary>The host in the roster, null when not in a game.</summary>
    public static CoopPlayer Host => InGame ? _mod.Session.Roster.Host : null;

    /// <summary>Everyone in the game by slot, this player included. Empty when not in a game.</summary>
    public static IReadOnlyList<CoopPlayer> Players => InGame ? _mod.Session.Roster.Players : Nobody;

    /// <summary>The session underneath, for anything this class doesn't cover. Null until ObenCoop is loaded.</summary>
    public static CoopSession Session => _mod?.Session;

    /// <summary>The lobby: host, join, lists, invites. Null until ObenCoop is loaded.</summary>
    public static CoopLobby Lobby => _mod?.Lobby;

    /// <summary>Where the other players are (area, position, facing). Null until ObenCoop is loaded.</summary>
    public static CoopPresence Presence => _mod?.Presence;

    /// <summary>Round trip to a player in milliseconds, -1 when unknown.</summary>
    public static int PingOf(CoopPlayer player) => _mod != null && player != null ? _mod.Session.PingOf(player) : -1;

    /// <summary>Hosting started, or the host let this game in.</summary>
    public static void OnStarted(ITracker owner, Action handler) => StartedHandlers.Add(owner, handler);

    /// <summary>A player joined. A joining game gets this once for everyone already there.</summary>
    public static void OnPlayerJoined(ITracker owner, Action<CoopPlayer> handler) => JoinedHandlers.Add(owner, handler);

    /// <summary>A player left, with the reason as a sentence.</summary>
    public static void OnPlayerLeft(ITracker owner, Action<CoopPlayer, string> handler) => LeftHandlers.Add(owner, handler);

    /// <summary>This game's co-op game ended: it left, was turned away, or lost the host.</summary>
    public static void OnEnded(ITracker owner, Action<SessionEnd> handler) => EndedHandlers.Add(owner, handler);

    /// <summary>
    /// Listens on a named channel (<see cref="ChannelNames"/>: "mymod.doors", up to 64 characters, no
    /// spaces). Every message other players send on that name arrives here, with who sent it.
    /// </summary>
    /// <exception cref="InvalidOperationException">ObenCoop isn't loaded, or the name is taken.</exception>
    public static void Listen(ITracker owner, string channel, MessageHandler handler) =>
        owner.Track(Require().Session.Router.Register(channel, handler));

    /// <summary>Sends to every other player. False when not in a game or the message can't go.</summary>
    public static bool Broadcast(string channel, MessageWriter payload, DeliveryMode mode = DeliveryMode.ReliableOrdered) =>
        InGame && _mod.Session.Broadcast(channel, payload, mode);

    /// <summary>Sends to one player (passed on by the host when both are guests).</summary>
    public static bool SendTo(CoopPlayer player, string channel, MessageWriter payload, DeliveryMode mode = DeliveryMode.ReliableOrdered) =>
        InGame && _mod.Session.SendTo(player, channel, payload, mode);

    /// <summary>Sends to the host only. False on the host itself.</summary>
    public static bool SendToHost(string channel, MessageWriter payload, DeliveryMode mode = DeliveryMode.ReliableOrdered) =>
        InGame && _mod.Session.SendToHost(channel, payload, mode);

    /// <summary>
    /// Receives big payloads (a save, a world) on a named channel; blob names are their own, apart from
    /// message names. <paramref name="received"/> gets the data complete and checked.
    /// </summary>
    /// <exception cref="InvalidOperationException">ObenCoop isn't loaded, or the name is taken.</exception>
    public static void ListenBlobs(ITracker owner, string channel, BlobHandler received,
        Action<BlobStatus> progress = null, Action<BlobStatus> failed = null) =>
        owner.Track(Require().Session.Blobs.Register(channel, received, progress, failed));

    /// <summary>
    /// Sends a byte array of any size (up to 32 MB) to one player on a named channel. Returns at once;
    /// the status follows the transfer, and <paramref name="finished"/> runs when it is done or failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">ObenCoop isn't loaded.</exception>
    public static BlobStatus SendBlob(CoopPlayer player, string channel, byte[] data, string name = "",
        Action<BlobStatus> progress = null, Action<BlobStatus> finished = null, int timeoutMs = 0) =>
        Require().Session.Blobs.Send(player, channel, data, name, progress, finished, timeoutMs);

    internal static void Attach(CoopMod mod)
    {
        _mod = mod;
        var session = mod.Session;
        session.Started += () => StartedHandlers.ForEach(h => h(), mod.Log, "Coop.Started");
        session.PlayerJoined += p => JoinedHandlers.ForEach(h => h(p), mod.Log, "Coop.PlayerJoined");
        session.PlayerLeft += (p, why) => LeftHandlers.ForEach(h => h(p, why), mod.Log, "Coop.PlayerLeft");
        session.Ended += end => EndedHandlers.ForEach(h => h(end), mod.Log, "Coop.Ended");
    }

    internal static void Detach(CoopMod mod)
    {
        if (_mod == mod)
            _mod = null;
    }

    private static CoopMod Require() =>
        _mod ?? throw new InvalidOperationException("ObenCoop isn't loaded. Add \"ObenCoop\" to the dependencies in mod.json.");
}
