using System;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Lobby;

/// <summary>One lobby found by a scan, with the verdict for this game.</summary>
public sealed class LobbyListing
{
    public LobbyListing(LobbyAddress address, LobbyInfo info, int pingMs)
    {
        Address = address;
        Info = info;
        PingMs = pingMs;
    }

    public LobbyAddress Address { get; }

    public LobbyInfo Info { get; }

    /// <summary>Milliseconds from the query to the answer, or -1 when unknown (Steam).</summary>
    public int PingMs { get; }

    public LobbyVerdict Verdict { get; internal set; }

    /// <summary>Why it doesn't fit, or null.</summary>
    public string Problem { get; internal set; }
}

/// <summary>
/// One way of finding and reaching games: direct IP (<see cref="DirectLobbyBackend"/>) or Steam
/// (<c>Matchmaking.MatchmakingBackend</c>). <see cref="CoopLobby"/> drives it and keeps the state
/// machine; a backend only does the platform's part. Every callback is called at most once, from
/// <see cref="Poll"/> or directly from the call that started it, never from another thread.
/// </summary>
public interface ILobbyBackend : IDisposable
{
    LobbyKind Kind { get; }

    /// <summary>The transport the session runs on for this kind of lobby.</summary>
    ITransport Transport { get; }

    /// <summary>Why this way of playing can't be used right now, as a sentence, or null when it can.</summary>
    string Unavailable { get; }

    /// <summary>
    /// Starts advertising a game the session already hosts on <see cref="Transport"/>. Calls
    /// <paramref name="done"/> with the lobby's address (for codes and invites) or an error.
    /// </summary>
    void Open(LobbyInfo info, Action<LobbyAddress, string> done);

    /// <summary>The hosted game changed (players came or went).</summary>
    void Update(LobbyInfo info);

    /// <summary>Stops advertising, or leaves a lobby joined through this backend. Safe to call when nothing is open.</summary>
    void Close();

    /// <summary>
    /// Looks a lobby up before joining. Calls <paramref name="done"/> with what the lobby advertises
    /// (null when it didn't answer), the address to hand to the session's Join, and an error (null when fine).
    /// </summary>
    void Lookup(LobbyAddress address, Action<LobbyInfo, string, string> done);

    /// <summary>
    /// Looks for games: every one found goes to <paramref name="found"/>, then <paramref name="done"/>
    /// runs with null or an error. <paramref name="only"/> asks one address instead (direct play).
    /// </summary>
    void Scan(LobbyAddress only, Action<LobbyListing> found, Action<string> done);

    void StopScan();

    /// <summary>Opens the platform's invite dialog. False when there is none (direct play shares a code instead).</summary>
    bool ShowInviteDialog();

    /// <summary>
    /// Picks up a lobby that was open before a hot reload of the mod (the session carried on). For a
    /// hosted lobby <paramref name="info"/> is what it advertised. False when the lobby can't be had back.
    /// </summary>
    bool Resume(LobbyAddress address, LobbyInfo info, bool hosting);

    /// <summary>Raised when the player accepted an invite outside the game (the Steam overlay or friends list).</summary>
    event Action<LobbyAddress> JoinRequested;

    void Poll(long now);
}
