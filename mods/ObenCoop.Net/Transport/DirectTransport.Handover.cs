using LiteNetLib;

namespace ObenCoop.Net.Transport;

public sealed partial class DirectTransport
{
    /// <summary>
    /// Lets go of the running connection without closing it and returns it as an opaque array of
    /// LiteNetLib objects (types from an assembly that is never reloaded), for the host mod to keep
    /// until the next copy of this assembly calls <see cref="Adopt"/>. Null when nothing is running.
    /// This transport is empty afterwards.
    /// </summary>
    public object[] Detach()
    {
        if (_manager == null)
            return null;
        Unhook(_listener);
        var parked = new object[] { _manager, _listener, _maxPeers };
        _manager = null;
        _listener = null;
        _peers.Clear();
        return parked;
    }

    /// <summary>
    /// Takes over a connection handed out by <see cref="Detach"/> (possibly by an older build of this
    /// assembly). Returns false when the array isn't one or its connection has stopped.
    /// </summary>
    public bool Adopt(object[] parked)
    {
        if (parked == null || parked.Length != 3
            || !(parked[0] is NetManager manager) || !(parked[1] is EventBasedNetListener listener) || !(parked[2] is int maxPeers))
            return false;
        if (!manager.IsRunning)
            return false;

        Stop();
        _manager = manager;
        _listener = listener;
        _maxPeers = maxPeers;
        Attach(listener);
        foreach (var peer in manager.ConnectedPeerList)
            _peers[peer.Id] = peer;
        return true;
    }

    /// <summary>Closes a connection that was detached and never adopted.</summary>
    public static void Abandon(object[] parked)
    {
        if (parked != null && parked.Length == 3 && parked[0] is NetManager manager)
            manager.Stop(true);
    }
}
