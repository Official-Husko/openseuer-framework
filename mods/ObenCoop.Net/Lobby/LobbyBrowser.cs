using System;
using System.Collections.Generic;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// The lobby list: scans one kind of lobby (the local network for direct play, friends' and public
/// lobbies on Steam) and keeps what answered, each with the verdict for this game. A newer scan
/// replaces the old results; answers to an abandoned scan are ignored.
/// </summary>
public sealed class LobbyBrowser
{
    private readonly CoopLobby _lobby;
    private readonly List<LobbyListing> _results = new List<LobbyListing>();
    private ILobbyBackend _scanning;
    private int _generation;

    internal LobbyBrowser(CoopLobby lobby)
    {
        _lobby = lobby;
    }

    /// <summary>What the last scan found, in the order the answers came.</summary>
    public IReadOnlyList<LobbyListing> Results => _results;

    public bool Scanning => _scanning != null;

    /// <summary>The kind of the last scan.</summary>
    public LobbyKind Kind { get; private set; }

    /// <summary>Why the last scan failed, or empty.</summary>
    public string Error { get; private set; } = "";

    /// <summary>Raised for each lobby found.</summary>
    public event Action<LobbyListing> Found;

    /// <summary>Raised when a scan is over.</summary>
    public event Action Finished;

    /// <summary>
    /// Starts a scan, dropping the previous results. <paramref name="only"/> asks one address
    /// instead of the whole network. False with <see cref="Error"/> set when it can't start.
    /// </summary>
    public bool Scan(LobbyKind kind, LobbyAddress only = default)
    {
        Stop();
        _results.Clear();
        Kind = kind;
        Error = "";
        string unavailable = _lobby.Unavailable(kind);
        if (unavailable != null)
        {
            Error = unavailable;
            return false;
        }

        var backend = _lobby.Backend(kind);
        int generation = ++_generation;
        _scanning = backend;
        backend.Scan(only, listing => OnFound(generation, listing), error => OnDone(generation, error));
        return Error.Length == 0;
    }

    public void Stop()
    {
        _generation++;
        _scanning?.StopScan();
        _scanning = null;
    }

    /// <summary>The listing a player picked by number (1 is the first), or null.</summary>
    public LobbyListing Pick(int number) => number >= 1 && number <= _results.Count ? _results[number - 1] : null;

    internal void Forget(ILobbyBackend backend)
    {
        if (_scanning == backend)
            Stop();
    }

    private void OnFound(int generation, LobbyListing listing)
    {
        if (generation != _generation)
            return;
        // A game on this machine answers on several routes (loopback and the network); keep the first.
        for (int i = 0; i < _results.Count; i++)
        {
            var known = _results[i];
            if ((listing.Info.Id != 0 && known.Info.Id == listing.Info.Id) || known.Address.Equals(listing.Address))
                return;
        }
        listing.Verdict = LobbyCheck.Check(listing.Info, _lobby.Session.Options, out string problem);
        listing.Problem = problem;
        _results.Add(listing);
        try { Found?.Invoke(listing); }
        catch (Exception e) { _lobby.Session.Log("A lobby list handler threw: " + e); }
    }

    private void OnDone(int generation, string error)
    {
        if (generation != _generation)
            return;
        _scanning = null;
        Error = error ?? "";
        try { Finished?.Invoke(); }
        catch (Exception e) { _lobby.Session.Log("A lobby list handler threw: " + e); }
    }
}
