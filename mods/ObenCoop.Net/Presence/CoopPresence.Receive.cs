using System;
using System.Collections.Generic;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Presence;

// The other players: samples in, stale players out, poses for drawing.
public sealed partial class CoopPresence
{
    private readonly List<PresenceRemote> _remotes = new List<PresenceRemote>();

    /// <summary>The other players that are sharing where they are, in the order they first showed up.</summary>
    public IReadOnlyList<PresenceRemote> Remotes => _remotes;

    /// <summary>Raised when a player's first sample arrives (or the first after they stopped sharing).</summary>
    public event Action<PresenceRemote> Appeared;

    /// <summary>Raised when a player stops sharing, goes stale, leaves the session, or the session ends.</summary>
    public event Action<PresenceRemote> Vanished;

    /// <summary>Raised when a player's samples name a different area than before (they changed level).</summary>
    public event Action<PresenceRemote> AreaChanged;

    public long Received { get; private set; }

    /// <summary>Samples thrown away because a value was out of range.</summary>
    public long Rejected { get; private set; }

    /// <summary>Samples thrown away because a newer one had already arrived.</summary>
    public long Late { get; private set; }

    /// <summary>The player's presence, or null when they aren't sharing.</summary>
    public PresenceRemote Find(CoopPlayer player)
    {
        for (int i = 0; i < _remotes.Count; i++)
        {
            if (_remotes[i].Player == player)
                return _remotes[i];
        }
        return null;
    }

    /// <summary>
    /// Where to draw a player right now: between two of their samples, <see cref="PresenceOptions.InterpolationDelayMs"/>
    /// behind the newest. False when they aren't sharing.
    /// </summary>
    public bool TryGetPose(CoopPlayer player, out PresencePose pose)
    {
        var remote = Find(player);
        if (remote == null)
        {
            pose = default;
            return false;
        }
        return remote.Track.Sample(_session.Options.Clock(), Options.InterpolationDelayMs, Options.MaxExtrapolationMs, out pose);
    }

    private void OnMessage(CoopPlayer sender, MessageReader reader)
    {
        byte kind = reader.ReadByte();
        if (kind == PresenceCodec.KindWithdrawn)
        {
            Remove(sender);
            return;
        }
        if (kind != PresenceCodec.KindSample)
        {
            Rejected++;
            return;
        }

        var remote = Find(sender);
        bool fresh = remote == null;
        if (fresh)
            remote = new PresenceRemote(sender);
        if (!PresenceCodec.TryReadSample(reader, remote.Names, out var sample))
        {
            Rejected++;
            return;
        }

        string areaBefore = remote.Area;
        if (!remote.Track.Add(sample, _session.Options.Clock()))
        {
            Late++;
            return;
        }
        Received++;

        if (fresh)
        {
            _remotes.Add(remote);
            Raise(Appeared, remote);
        }
        else if (!ReferenceEquals(areaBefore, sample.Area) && !string.Equals(areaBefore, sample.Area, StringComparison.Ordinal))
        {
            Raise(AreaChanged, remote);
        }
    }

    private void Prune(long now)
    {
        for (int i = _remotes.Count - 1; i >= 0; i--)
        {
            if (i < _remotes.Count && now - _remotes[i].LastSeen > Options.StaleAfterMs)
                RemoveAt(i);
        }
    }

    private void Remove(CoopPlayer player)
    {
        for (int i = 0; i < _remotes.Count; i++)
        {
            if (_remotes[i].Player == player)
            {
                RemoveAt(i);
                return;
            }
        }
    }

    private void RemoveAt(int index)
    {
        var remote = _remotes[index];
        _remotes.RemoveAt(index);
        Raise(Vanished, remote);
    }

    private void OnPlayerLeft(CoopPlayer player, string reason) => Remove(player);

    private void OnEnded(SessionEnd end)
    {
        _sharing = false;
        _hasSent = false;
        while (_remotes.Count > 0)
            RemoveAt(_remotes.Count - 1);
    }

    private void Raise(Action<PresenceRemote> handlers, PresenceRemote remote)
    {
        try { handlers?.Invoke(remote); }
        catch (Exception e) { _session.Log("A presence handler threw: " + e); }
    }
}
