// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Blobs;

/// <summary>
/// Handles a blob that arrived complete and checked. <paramref name="data"/> is the payload, exactly
/// as long as it was sent, and belongs to the handler. Raised from <see cref="CoopSession.Poll"/>.
/// </summary>
public delegate void BlobHandler(CoopPlayer sender, byte[] data, BlobStatus blob);

/// <summary>
/// Sends byte arrays of any size to another player and receives them. The data is compressed
/// (deflate; sent as it is when that doesn't help), cut into chunks that fit a reliable packet and
/// protected by a CRC-32 over the whole payload. The receiver acknowledges every few chunks and the
/// sender never has more than a window of chunks unacknowledged, so a slow receiver is not flooded;
/// when the last chunk is in, the receiver unpacks and checks it and tells the sender the result.
/// Compressing, unpacking and the CRC run on worker threads; everything else, and every callback,
/// runs in <see cref="CoopSession.Poll"/>.
///
/// A blob goes to one player (a client's blob to another client is relayed by the host). Receivers
/// register a handler per blob channel with <see cref="Register(byte, BlobHandler, Action{BlobStatus}, Action{BlobStatus})"/>;
/// blob channels are their own numbering, separate from message channels. Mods use named channels
/// (<see cref="ChannelNames"/>) instead, the same names as for their messages if they like. Any number of transfers can run at once, each with
/// its id. A transfer fails with a <see cref="BlobFailure"/> when it is cancelled, times out, is
/// refused or arrives damaged, or when either player leaves; both sides are told. Transfers do not
/// survive a hot reload of the session.
/// Files: this one is the shared part, Send and Receive hold each end's side.
/// </summary>
public sealed partial class BlobTransfer
{
    private const int MaxNameLength = 100;

    private readonly CoopSession _session;
    private readonly Dictionary<uint, BlobListener> _listeners = new Dictionary<uint, BlobListener>();
    private readonly List<BlobStatus> _outgoing = new List<BlobStatus>();
    private readonly Dictionary<long, BlobStatus> _incoming = new Dictionary<long, BlobStatus>();
    private int _lastId;
    private long _incomingBytes;

    // Test seams: flip the CRC sent with an offer, or damage a chunk's bytes before it is sent.
    internal uint TamperCrc;
    internal Action<byte[], int, int> TamperChunk;

    internal BlobTransfer(CoopSession session)
    {
        _session = session;
        session.Router.Register(ChannelIds.Blob, OnPacket);
        session.PlayerLeft += OnPlayerLeft;
        session.Ended += OnEnded;
    }

    /// <summary>Transfers this game is sending that haven't finished.</summary>
    public int OutgoingCount => _outgoing.Count;

    /// <summary>Transfers this game is receiving that haven't finished.</summary>
    public int IncomingCount => _incoming.Count;

    /// <summary>
    /// Starts receiving blobs on a blob channel. <paramref name="received"/> gets each complete blob;
    /// <paramref name="progress"/> is called as chunks arrive, <paramref name="failed"/> when a blob
    /// fails. One listener per channel; offers for a channel nobody listens on are refused. Dispose the
    /// result to stop (hand it to the mod's tracker); blobs still arriving on the channel are cancelled.
    /// </summary>
    /// <exception cref="InvalidOperationException">The channel already has a listener.</exception>
    public IDisposable Register(byte channel, BlobHandler received, Action<BlobStatus> progress = null, Action<BlobStatus> failed = null) =>
        Register(channel, "", received, progress, failed);

    /// <summary>The same for a named channel (<see cref="ChannelNames"/>).</summary>
    /// <exception cref="ArgumentException">The name isn't a valid channel name.</exception>
    /// <exception cref="InvalidOperationException">The channel already has a listener, or another name has the same key.</exception>
    public IDisposable Register(string channel, BlobHandler received, Action<BlobStatus> progress = null, Action<BlobStatus> failed = null) =>
        Register(ChannelNames.Key(channel), channel, received, progress, failed);

    public bool IsRegistered(byte channel) => _listeners.ContainsKey(channel);

    public bool IsRegistered(string channel) => _listeners.ContainsKey(ChannelNames.Key(channel));

    private IDisposable Register(uint key, string name, BlobHandler received, Action<BlobStatus> progress, Action<BlobStatus> failed)
    {
        if (received == null)
            throw new ArgumentNullException(nameof(received));
        if (_listeners.TryGetValue(key, out var taken))
        {
            throw new InvalidOperationException(taken.Name == name
                ? $"Blob channel {Describe(key, name)} already has a listener."
                : $"Blob channel '{name}' has the same key as '{taken.Name}'; pick another name.");
        }
        var listener = new BlobListener(this, key, name, received, progress, failed);
        _listeners[key] = listener;
        return listener;
    }

    /// <summary>Cancels a transfer on both sides. False when it had already finished.</summary>
    public bool Cancel(BlobStatus blob)
    {
        if (blob == null || blob.IsFinished)
            return false;
        Fail(blob, BlobFailure.Cancelled, "The transfer was cancelled.", true);
        return true;
    }

    internal void Poll(long now)
    {
        if (_outgoing.Count > 0)
            PumpOutgoing(now);
        if (_incoming.Count > 0)
            PumpIncoming(now);
    }

    internal void Unregister(BlobListener listener)
    {
        if (!_listeners.TryGetValue(listener.Key, out var current) || current != listener)
            return;
        _listeners.Remove(listener.Key);
        // A fresh list: the failure callbacks run user code that may change the transfers.
        var affected = new List<BlobStatus>();
        foreach (var blob in _incoming.Values)
        {
            if (blob.Listener == listener)
                affected.Add(blob);
        }
        foreach (var blob in affected)
            Fail(blob, BlobFailure.NoHandler, "The receiver stopped listening for this kind of data.", true);
    }

    private void OnPacket(CoopPlayer sender, MessageReader reader)
    {
        var kind = (BlobKind)reader.ReadByte();
        int id = reader.ReadInt32();
        switch (kind)
        {
            case BlobKind.Open: OnOpen(sender, id, reader); break;
            case BlobKind.Chunk: OnChunk(sender, id, reader); break;
            case BlobKind.Ack: OnAck(sender, id, reader); break;
            case BlobKind.Done: OnDone(sender, id); break;
            case BlobKind.AbortBySender: OnAbortBySender(sender, id, reader); break;
            case BlobKind.AbortByReceiver: OnAbortByReceiver(sender, id, reader); break;
            default: _session.Log($"Ignored a blob packet of unknown kind {(int)kind} from {sender}."); break;
        }
    }

    private void OnPlayerLeft(CoopPlayer player, string reason) =>
        FailWhere(b => b.Peer == player, BlobFailure.PeerLeft, player.Name + " left the game.");

    private void OnEnded(SessionEnd end) =>
        FailWhere(b => true, BlobFailure.SessionEnded, "The session ended: " + end.Message);

    private void FailWhere(Func<BlobStatus, bool> match, BlobFailure failure, string message)
    {
        var all = new List<BlobStatus>(_outgoing);
        all.AddRange(_incoming.Values);
        foreach (var blob in all)
        {
            if (match(blob))
                Fail(blob, failure, message, false);
        }
    }

    // Ends a transfer as failed on this side, tells the other side when asked, and raises the callback.
    private void Fail(BlobStatus blob, BlobFailure failure, string message, bool tellPeer)
    {
        if (blob.IsFinished)
            return;
        Release(blob);
        blob.State = BlobState.Failed;
        blob.Failure = failure;
        blob.Message = message;
        _session.Log($"{blob} failed ({failure}): {message}");

        if (tellPeer)
        {
            var packet = BlobWire.Start(blob.Direction == BlobDirection.Sending ? BlobKind.AbortBySender : BlobKind.AbortByReceiver, blob.Id);
            packet.WriteByte((byte)failure);
            packet.WriteString(Trim(message));
            Transmit(blob.Peer, packet);
        }
        Invoke(blob.Direction == BlobDirection.Sending ? blob.OnFinished : blob.Listener?.Failed, blob);
    }

    // Forgets a finished transfer and lets go of its buffers.
    private void Release(BlobStatus blob)
    {
        if (blob.Direction == BlobDirection.Sending)
            _outgoing.Remove(blob);
        else if (_incoming.Remove(Key(blob.Peer, blob.Id)))
            _incomingBytes -= blob.Reserved;
        blob.Reserved = 0;
        blob.Wire = null;
        
    }

    // Control packets and chunks are not subject to the send queue limit; chunks check for room first.
    private bool Transmit(CoopPlayer to, MessageWriter packet)
    {
        try
        {
            return _session.SendTo(to, ChannelIds.Blob, packet, Transport.DeliveryMode.ReliableOrdered, false);
        }
        finally
        {
            MessagePool.Return(packet);
        }
    }

    private void Invoke(Action<BlobStatus> callback, BlobStatus blob)
    {
        if (callback == null)
            return;
        try { callback(blob); }
        catch (Exception e) { _session.Log("A blob callback threw: " + e); }
    }

    private static long Key(CoopPlayer sender, int id) => ((long)sender.Slot << 32) | (uint)id;

    private static string Describe(uint key, string name) => name.Length > 0 ? "'" + name + "'" : key.ToString(CultureInfo.InvariantCulture);

    private static string Trim(string text) =>
        text != null && text.Length > 300 ? text.Substring(0, 300) : text ?? "";

    private static string Size(long bytes) =>
        bytes >= 1024 * 1024 ? (bytes / (1024.0 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture) + " MB" : bytes >= 1024 ? (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB" : bytes + " bytes";
}

/// <summary>A registration of a blob handler; disposing it stops listening.</summary>
internal sealed class BlobListener : IDisposable
{
    private readonly BlobTransfer _owner;

    public BlobListener(BlobTransfer owner, uint key, string name, BlobHandler received, Action<BlobStatus> progress, Action<BlobStatus> failed)
    {
        _owner = owner;
        Key = key;
        Name = name;
        Received = received;
        Progress = progress;
        Failed = failed;
    }

    public uint Key { get; }
    public string Name { get; }
    public BlobHandler Received { get; }
    public Action<BlobStatus> Progress { get; }
    public Action<BlobStatus> Failed { get; }

    public void Dispose() => _owner.Unregister(this);
}
