using System;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Messaging;

/// <summary>
/// Handles a message that arrived on a channel. <paramref name="sender"/> is the player who sent it;
/// <paramref name="reader"/> is positioned at the payload and is only valid during the call.
/// </summary>
public delegate void MessageHandler(Player sender, MessageReader reader);

/// <summary>
/// Maps channel ids to handlers. A channel is one feature's message stream (chat, positions, ...):
/// one handler per channel, so two features can't read each other's messages by accident. A handler
/// that throws is logged through <see cref="Error"/> and doesn't stop the others.
/// </summary>
public sealed class MessageRouter
{
    private readonly MessageHandler[] _handlers = new MessageHandler[256];

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

    /// <summary>Delivers a message to the channel's handler. Returns false when it was dropped.</summary>
    public bool Dispatch(byte channel, Player sender, MessageReader reader)
    {
        var handler = _handlers[channel];
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
}
