using System;
using System.Collections.Generic;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Messaging;

/// <summary>
/// Handles a message that arrived on a channel. <paramref name="sender"/> is the player who sent it;
/// <paramref name="reader"/> is positioned at the payload and is only valid during the call.
/// </summary>
public delegate void MessageHandler(CoopPlayer sender, MessageReader reader);

/// <summary>
/// Maps channel ids to handlers. A channel is one feature's message stream (chat, positions, ...):
/// one handler per channel, so two features can't read each other's messages by accident. A handler
/// that throws is logged through <see cref="Error"/> and doesn't stop the others. Mods use named
/// channels (<see cref="ChannelNames"/>), which all share <see cref="ChannelIds.Named"/>.
/// </summary>
public sealed class MessageRouter
{
    private readonly MessageHandler[] _handlers = new MessageHandler[256];
    private readonly Dictionary<uint, Named> _named = new Dictionary<uint, Named>();

    /// <summary>Messages that arrived for a channel nobody listens on, or whose handler threw.</summary>
    public long Dropped { get; private set; }

    public long Delivered { get; private set; }

    /// <summary>Raised with the channel and the exception when a handler throws or a message is malformed.</summary>
    public event Action<byte, Exception> Error;

    internal void CountDropped() => Dropped++;

    public bool IsRegistered(byte channel) => _handlers[channel] != null;

    /// <summary>
    /// Starts listening on a channel. Dispose the result to stop (mods pass it to their tracker so
    /// it goes away on unload).
    /// </summary>
    /// <exception cref="InvalidOperationException">The channel already has a handler.</exception>
    public IDisposable Register(byte channel, MessageHandler handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (_handlers[channel] != null)
            throw new InvalidOperationException($"Channel {channel} already has a handler.");
        _handlers[channel] = handler;
        return new Registration(this, channel, handler);
    }

    public bool IsRegistered(string channel) => _named.ContainsKey(ChannelNames.Key(channel));

    /// <summary>
    /// Starts listening on a named channel. Dispose the result to stop (mods pass it to their tracker).
    /// <paramref name="handler"/> gets the payload without the channel's key.
    /// </summary>
    /// <exception cref="ArgumentException">The name isn't a valid channel name (<see cref="ChannelNames.Check"/>).</exception>
    /// <exception cref="InvalidOperationException">The channel already has a handler, or another name has the same key.</exception>
    public IDisposable Register(string channel, MessageHandler handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        uint key = ChannelNames.Key(channel);
        if (_named.TryGetValue(key, out var taken))
        {
            throw new InvalidOperationException(taken.Name == channel
                ? $"Channel '{channel}' already has a handler."
                : $"Channel '{channel}' has the same key as '{taken.Name}'; pick another name.");
        }
        var named = new Named(channel, handler);
        _named[key] = named;
        return new NamedRegistration(this, key, named);
    }

    /// <summary>Delivers a message to the channel's handler. Returns false when it was dropped.</summary>
    public bool Dispatch(byte channel, CoopPlayer sender, MessageReader reader)
    {
        var handler = _handlers[channel];
        if (channel == ChannelIds.Named && handler == null)
            return DispatchNamed(sender, reader);
        if (handler == null)
        {
            Dropped++;
            return false;
        }
        try
        {
            handler(sender, reader);
            Delivered++;
            return true;
        }
        catch (Exception e)
        {
            Dropped++;
            Error?.Invoke(channel, e);
            return false;
        }
    }

    // A name nobody here listens on is dropped quietly, like an unknown numbered channel: the other
    // player may run a mod feature this one has switched off.
    private bool DispatchNamed(CoopPlayer sender, MessageReader reader)
    {
        Named named = null;
        try
        {
            if (!_named.TryGetValue(reader.ReadUInt32(), out named))
            {
                Dropped++;
                return false;
            }
            named.Handler(sender, reader);
            Delivered++;
            return true;
        }
        catch (Exception e)
        {
            Dropped++;
            Error?.Invoke(ChannelIds.Named, named == null ? e
                : new InvalidOperationException($"The handler of channel '{named.Name}' failed: {e.Message}", e));
            return false;
        }
    }

    private sealed class Registration : IDisposable
    {
        private readonly MessageRouter _router;
        private readonly byte _channel;
        private MessageHandler _handler;

        public Registration(MessageRouter router, byte channel, MessageHandler handler)
        {
            _router = router;
            _channel = channel;
            _handler = handler;
        }

        public void Dispose()
        {
            // Only remove our own handler; the channel may have been taken again since.
            if (_handler != null && _router._handlers[_channel] == _handler)
                _router._handlers[_channel] = null;
            _handler = null;
        }
    }

    private sealed class Named
    {
        public Named(string name, MessageHandler handler)
        {
            Name = name;
            Handler = handler;
        }

        public string Name { get; }
        public MessageHandler Handler { get; }
    }

    private sealed class NamedRegistration : IDisposable
    {
        private readonly MessageRouter _router;
        private readonly uint _key;
        private Named _named;

        public NamedRegistration(MessageRouter router, uint key, Named named)
        {
            _router = router;
            _key = key;
            _named = named;
        }

        public void Dispose()
        {
            if (_named != null && _router._named.TryGetValue(_key, out var current) && current == _named)
                _router._named.Remove(_key);
            _named = null;
        }
    }
}
