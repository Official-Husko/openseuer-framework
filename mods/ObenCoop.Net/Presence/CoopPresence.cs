// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Presence;

/// <summary>
/// Where every player is, shared at a low rate: each game publishes its own player's area, position,
/// heading, velocity and a few flags, and gets the others' as <see cref="Remotes"/> with enough
/// history to draw them smoothly (<see cref="TryGetPose"/>). Samples go out unreliable on
/// <see cref="ChannelIds.Presence"/>, 10 a second while moving and once a second while still; the
/// host relays them like any other message. This is the data layer only: puppets that show the other
/// players are built on top of it.
/// Split in two: this file publishes and sends, Receive keeps the remote players.
/// </summary>
public sealed partial class CoopPresence : IDisposable
{
    private const float YawThreshold = 0.5f;

    private readonly CoopSession _session;
    private readonly IDisposable _channel;

    private bool _published;
    private bool _sharing;
    private long _publishedAt;
    private long _sentAt;
    private bool _sendNow;
    private bool _jumped;
    private ushort _sequence;
    private PresenceSample _local;
    private PresenceSample _lastSent;
    private bool _hasSent;

    public CoopPresence(CoopSession session, PresenceOptions options = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Options = options ?? new PresenceOptions();
        _channel = session.Router.Register(ChannelIds.Presence, OnMessage);
        session.PlayerJoined += OnPlayerJoined;
        session.PlayerLeft += OnPlayerLeft;
        session.Ended += OnEnded;
    }

    public PresenceOptions Options { get; }

    /// <summary>True while samples go out: in a session, and <see cref="Publish"/> was called recently.</summary>
    public bool IsSharing => _sharing;

    /// <summary>The last sample this game sent.</summary>
    public PresenceSample LastSent => _lastSent;

    public long Sent { get; private set; }

    /// <summary>
    /// Sets where the local player is. Call it every frame or at least as often as samples go out;
    /// the sending rate is this class's business. When calls stop for <see cref="PresenceOptions.PublishTimeoutMs"/>
    /// (main menu, loading) samples stop too and the others drop this player.
    /// </summary>
    /// <exception cref="ArgumentException">The area is empty or too long, or a number is out of range.</exception>
    public void Publish(string area, PresenceVector position, float yaw, PresenceVector velocity, PresenceFlags flags)
    {
        if (!PresenceCodec.IsValid(area, position, yaw, velocity, out string problem))
            throw new ArgumentException(problem);

        if (_hasSent && (!string.Equals(area, _lastSent.Area, StringComparison.Ordinal)
                         || position.DistanceTo(_lastSent.Position) > Options.TeleportDistance))
            _jumped = true;

        _local.Area = area;
        _local.Position = position;
        _local.Yaw = yaw;
        _local.Velocity = velocity;
        _local.Flags = flags & ~PresenceFlags.Teleported;
        _published = true;
        _publishedAt = _session.Options.Clock();
    }

    /// <summary>Stops sharing at once and tells the others, instead of letting the samples time out.</summary>
    public void Withdraw()
    {
        _published = false;
        if (_sharing)
            StopSharing();
    }

    /// <summary>Sends a sample when one is due and drops stale remote players. Call once per frame, after the session's Poll.</summary>
    public void Tick()
    {
        long now = _session.Options.Clock();
        Prune(now);

        bool live = _session.IsActive && _published && now - _publishedAt <= Options.PublishTimeoutMs;
        if (!live)
        {
            if (_sharing)
                StopSharing();
            return;
        }

        _sharing = true;
        int interval = _sendNow || !_hasSent || Changed() ? Options.SendIntervalMs : Options.IdleIntervalMs;
        if (now - _sentAt < interval)
            return;
        Send(now);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _session.PlayerJoined -= OnPlayerJoined;
        _session.PlayerLeft -= OnPlayerLeft;
        _session.Ended -= OnEnded;
        _remotes.Clear();
    }

    private void Send(long now)
    {
        _local.Sequence = ++_sequence;
        _local.Time = (uint)now;
        if (_jumped)
            _local.Flags |= PresenceFlags.Teleported;

        var writer = MessagePool.RentWriter();
        try
        {
            PresenceCodec.Write(writer, _local);
            _session.Broadcast(ChannelIds.Presence, writer, DeliveryMode.Unreliable);
        }
        finally
        {
            MessagePool.Return(writer);
        }

        _lastSent = _local;
        _hasSent = true;
        _local.Flags &= ~PresenceFlags.Teleported;
        _jumped = false;
        _sendNow = false;
        _sentAt = now;
        Sent++;
    }

    // Anything worth more than the idle rate: a move, a turn, a different area or flags.
    private bool Changed() =>
        _local.Flags != (_lastSent.Flags & ~PresenceFlags.Teleported)
        || _jumped
        || _local.Position.DistanceTo(_lastSent.Position) > Options.StillThreshold
        || Math.Abs(PresenceTrack.DeltaAngle(_lastSent.Yaw, _local.Yaw)) > YawThreshold
        || !string.Equals(_local.Area, _lastSent.Area, StringComparison.Ordinal);

    private void StopSharing()
    {
        _sharing = false;
        _hasSent = false;
        if (!_session.IsActive)
            return;
        var writer = MessagePool.RentWriter();
        try
        {
            PresenceCodec.WriteWithdrawn(writer);
            _session.Broadcast(ChannelIds.Presence, writer);
        }
        finally
        {
            MessagePool.Return(writer);
        }
    }

    private void OnPlayerJoined(CoopPlayer player) => _sendNow = true;
}
