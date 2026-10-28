// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>
/// A saved on/off switch on the player's stats that mods hold rather than set: it stays on while any mod
/// holds it, is put back on after level loads, is switched back to its earlier value while the game
/// writes a save, and returns to that value when the last mod lets go.
/// </summary>
internal sealed class FlagClaim
{
    private readonly string _name;
    private readonly Func<PlayerStats, bool> _read;
    private readonly Action<PlayerStats, bool> _write;
    private readonly List<object> _holders = new List<object>();
    private bool _before;
    private bool _parked;

    // The stats object the flag was last switched on for; each level builds a new one.
    private PlayerStats _appliedTo;

    public FlagClaim(string name, Func<PlayerStats, bool> read, Action<PlayerStats, bool> write)
    {
        _name = name;
        _read = read;
        _write = write;
    }

    public bool Held => _holders.Count > 0;

    public IDisposable Hold(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var token = new object();
        var stats = PlayerStats.instance;
        if (stats != null && (_holders.Count == 0 || stats != _appliedTo))
            _before = _read(stats);
        _holders.Add(token);
        if (stats != null)
        {
            _write(stats, true);
            _appliedTo = stats;
        }
        return owner.Track(Disposable.Create(() => Release(token)));
    }

    /// <summary>A new level's stats came from the save (with the flag parked); switch it on again.</summary>
    public void Reapply() => Guard(() =>
    {
        var stats = PlayerStats.instance;
        _parked = false;
        if (_holders.Count == 0 || stats == null || stats == _appliedTo)
            return;
        _appliedTo = stats;
        _before = _read(stats);
        if (!_before)
            _write(stats, true);
    });

    /// <summary>The game is about to write a save: write the value from before the mods held it.</summary>
    public void Park() => Guard(() =>
    {
        var stats = PlayerStats.instance;
        if (_holders.Count == 0 || stats == null || !_read(stats))
            return;
        _write(stats, _before);
        _parked = true;
    });

    public void Unpark() => Guard(() =>
    {
        if (!_parked)
            return;
        _parked = false;
        var stats = PlayerStats.instance;
        if (_holders.Count > 0 && stats != null)
            _write(stats, true);
    });

    /// <summary>The framework unloads: let go of everything.</summary>
    public void Clear()
    {
        _appliedTo = null;
        if (_holders.Count == 0)
            return;
        _holders.Clear();
        var stats = PlayerStats.instance;
        if (stats != null)
            Guard(() => _write(stats, _before));
    }

    private void Release(object token)
    {
        if (!_holders.Remove(token) || _holders.Count > 0)
            return;
        var stats = PlayerStats.instance;
        if (stats != null)
            Guard(() => _write(stats, _before));
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { GameEvents.Log.Error($"Keeping {_name} failed", e); }
    }
}
