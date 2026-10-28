using System;
using System.Collections.Generic;

namespace ObenCoop.Net.Session;

/// <summary>
/// Flood control for received packets. Each peer may have a number of packets processed per poll
/// (the budget); a packet over the budget is copied into a pooled buffer and waits in that peer's
/// queue, and the queue is worked off from the next poll on, oldest first. While a peer has
/// anything queued, its new packets queue up behind it, so order never changes. Budgets are per
/// peer: one noisy peer uses up its own and nobody else's.
/// </summary>
internal sealed class InboundQueue
{
    private readonly Dictionary<int, Lane> _lanes = new Dictionary<int, Lane>();
    private readonly List<int> _scratch = new List<int>();
    private readonly PacketBuffers _buffers = new PacketBuffers();

    /// <summary>Packets that went through a queue instead of being handled at once, since the start.</summary>
    public long Deferred { get; private set; }

    /// <summary>Packets waiting in all queues now.</summary>
    public int Waiting { get; private set; }

    /// <summary>Starts a poll: every peer gets its full budget again.</summary>
    public void BeginPoll()
    {
        foreach (var lane in _lanes.Values)
            lane.Used = 0;
    }

    /// <summary>True when the packet can be handled right now (nothing queued ahead of it and budget left).</summary>
    public bool TryAdmit(int peer, int budget)
    {
        var lane = LaneOf(peer);
        if (lane.Queue.Count > 0 || (budget > 0 && lane.Used >= budget))
            return false;
        lane.Used++;
        return true;
    }

    /// <summary>Copies the packet to the end of the peer's queue. False when the queue is at its limit.</summary>
    public bool Enqueue(int peer, ArraySegment<byte> data, int limit)
    {
        var lane = LaneOf(peer);
        if (limit > 0 && lane.Queue.Count >= limit)
            return false;
        var buffer = _buffers.Rent(data.Count);
        Buffer.BlockCopy(data.Array, data.Offset, buffer, 0, data.Count);
        lane.Queue.Enqueue(new Packet(buffer, data.Count));
        Waiting++;
        Deferred++;
        return true;
    }

    /// <summary>Handles queued packets, each peer up to what is left of its budget.</summary>
    public void Drain(int budget, Action<int, ArraySegment<byte>> handle)
    {
        if (Waiting == 0)
            return;
        _scratch.Clear();
        _scratch.AddRange(_lanes.Keys);
        foreach (int peer in _scratch)
        {
            while (_lanes.TryGetValue(peer, out var lane) && lane.Queue.Count > 0 && (budget <= 0 || lane.Used < budget))
            {
                lane.Used++;
                Handle(lane, peer, handle);
            }
        }
    }

    /// <summary>Handles everything still queued for a peer, ignoring the budget, and forgets the peer.</summary>
    public void Flush(int peer, Action<int, ArraySegment<byte>> handle)
    {
        while (_lanes.TryGetValue(peer, out var lane) && lane.Queue.Count > 0)
            Handle(lane, peer, handle);
        _lanes.Remove(peer);
    }

    /// <summary>Drops everything that is queued, for every peer.</summary>
    public void Clear()
    {
        foreach (var lane in _lanes.Values)
        {
            while (lane.Queue.Count > 0)
                _buffers.Return(lane.Queue.Dequeue().Buffer);
        }
        _lanes.Clear();
        Waiting = 0;
    }

    private void Handle(Lane lane, int peer, Action<int, ArraySegment<byte>> handle)
    {
        var packet = lane.Queue.Dequeue();
        Waiting--;
        try
        {
            handle(peer, new ArraySegment<byte>(packet.Buffer, 0, packet.Count));
        }
        finally
        {
            _buffers.Return(packet.Buffer);
        }
    }

    private Lane LaneOf(int peer)
    {
        if (!_lanes.TryGetValue(peer, out var lane))
            _lanes[peer] = lane = new Lane();
        return lane;
    }

    private sealed class Lane
    {
        public readonly Queue<Packet> Queue = new Queue<Packet>();
        public int Used;
    }

    private readonly struct Packet
    {
        public Packet(byte[] buffer, int count)
        {
            Buffer = buffer;
            Count = count;
        }

        public byte[] Buffer { get; }
        public int Count { get; }
    }
}
