namespace ObenCoop.Net.Transport.P2P;

// Hot reload. The network's sessions live in the platform (the Steam client), not in this assembly,
// so carrying a connection over is only a matter of remembering who is who: plain numbers.
public sealed partial class P2PTransport
{
    private const string HandoverTag = "ObenCoop.P2P/1";

    /// <summary>
    /// Lets go of the running connections without saying goodbye and returns them as plain data for the
    /// next copy of this assembly (<see cref="Adopt"/>). Null when nothing is running. Empty afterwards.
    /// </summary>
    public object[] Detach()
    {
        if (!_running)
            return null;
        var ids = new ulong[_peers.Count];
        var numbers = new int[_peers.Count];
        for (int i = 0; i < _peers.Count; i++)
        {
            ids[i] = _peers[i].Id;
            numbers[i] = _peers[i].Peer;
        }
        var parked = new object[] { HandoverTag, ids, numbers, _hosting, _hostId, _maxPeers, _nextPeer };
        _peers.Clear();
        _byId.Clear();
        _events.Clear();
        _running = false;
        _hosting = false;
        _hostId = 0;
        return parked;
    }

    /// <summary>Takes over connections handed out by <see cref="Detach"/>. False when the array isn't one.</summary>
    public bool Adopt(object[] parked)
    {
        if (!IsParked(parked)
            || !(parked[1] is ulong[] ids) || !(parked[2] is int[] numbers) || ids.Length != numbers.Length
            || !(parked[3] is bool hosting) || !(parked[4] is ulong hostId) || !(parked[5] is int maxPeers) || !(parked[6] is int nextPeer))
            return false;

        Stop();
        long now = _clock();
        _running = true;
        _hosting = hosting;
        _hostId = hostId;
        _maxPeers = maxPeers;
        _nextPeer = nextPeer;
        _connectStarted = now;
        for (int i = 0; i < ids.Length; i++)
        {
            var peer = new P2PPeer(ids[i], numbers[i], now);
            _peers.Add(peer);
            _byId[peer.Id] = peer;
        }
        return true;
    }

    /// <summary>Says goodbye to and closes connections that were detached and never adopted.</summary>
    public static void Abandon(object[] parked, IP2PNetwork net)
    {
        if (!IsParked(parked) || !(parked[1] is ulong[] ids) || net == null)
            return;
        var bye = new[] { P2PWire.Bye };
        foreach (ulong id in ids)
        {
            net.Send(id, bye, 1, true);
            net.Close(id);
        }
    }

    /// <summary>True when the array came from <see cref="Detach"/>.</summary>
    public static bool IsParked(object[] parked) =>
        parked != null && parked.Length == 7 && parked[0] is string tag && tag == HandoverTag;
}
