using System;
using System.Collections.Generic;

namespace ObenCoop.Net.Lobby.Matchmaking;

// Lobby lists: the lobbies friends playing the game are in, plus public ObenCoop lobbies. A friend's
// lobby has to be asked for its data first; each one is listed when its data says it is ObenCoop.
public sealed partial class MatchmakingBackend
{
    private readonly List<ulong> _asked = new List<ulong>();
    private readonly List<ulong> _reported = new List<ulong>();
    private readonly List<ulong> _friendLobbies = new List<ulong>();
    private Action<LobbyListing> _found;
    private Action<string> _scanDone;
    private long _scanUntil;
    private int _scanGeneration;

    /// <summary>How long a scan waits for lobby data before it is over.</summary>
    public int ScanDurationMs { get; set; } = 3000;

    public void Scan(LobbyAddress only, Action<LobbyListing> found, Action<string> done)
    {
        StopScan();
        _found = found;
        _scanDone = done;
        _scanUntil = _clock() + ScanDurationMs;
        int generation = ++_scanGeneration;

        if (only.Kind == LobbyKind.Steam)
        {
            Ask(only.LobbyId);
            return;
        }

        _matchmaking.FriendLobbies(_friendLobbies);
        foreach (ulong lobby in _friendLobbies)
            Ask(lobby);
        _matchmaking.FindLobbies(GameKey, GameTag, lobbies =>
        {
            if (generation != _scanGeneration || _scanDone == null || lobbies == null)
                return;
            foreach (ulong lobby in lobbies)
                Ask(lobby);
        });
    }

    public void StopScan()
    {
        _scanGeneration++;
        _found = null;
        _scanDone = null;
        _asked.Clear();
        _reported.Clear();
    }

    private void Ask(ulong lobby)
    {
        if (lobby == 0 || _asked.Contains(lobby))
            return;
        _asked.Add(lobby);
        // Data may already be known (public list results, a lobby seen before); otherwise it comes as LobbyChanged.
        if (!TryReport(lobby))
            _matchmaking.RequestData(lobby);
    }

    private void OnScanData(ulong lobby)
    {
        if (_scanDone != null && _asked.Contains(lobby))
            TryReport(lobby);
    }

    private bool TryReport(ulong lobby)
    {
        if (_reported.Contains(lobby))
            return true;
        if (_matchmaking.GetData(lobby, GameKey) != GameTag || !LobbyInfo.TryFromBase64(_matchmaking.GetData(lobby, InfoKey), out var info))
            return false;
        _reported.Add(lobby);
        _found?.Invoke(new LobbyListing(LobbyAddress.Steam(lobby), info, -1));
        return true;
    }

    private void PollScan(long now)
    {
        if (_scanDone == null || now < _scanUntil)
            return;
        var done = _scanDone;
        StopScan();
        done(null);
    }
}
