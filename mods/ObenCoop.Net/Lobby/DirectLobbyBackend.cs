using System;
using System.Net;
using LiteNetLib;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// Lobbies for direct play. Hosting means answering lobby queries on the game's own UDP port with
/// the <see cref="LobbyInfo"/>; looking up an address asks it directly; a scan broadcasts on the
/// local network and asks this machine's own ports, so a second copy of the game shows up too.
/// Codes and invites carry this machine's network address, or a public one the player gives.
/// </summary>
public sealed class DirectLobbyBackend : ILobbyBackend
{
    private readonly DirectTransport _transport;
    private readonly LobbyScanner _scanner = new LobbyScanner();
    private readonly Func<long> _clock;

    private byte[] _reply;

    private Action<LobbyInfo, string, string> _lookupDone;
    private LobbyAddress _lookupAddress;
    private IPEndPoint _lookupEndPoint;
    private long _lookupUntil;

    private Action<LobbyListing> _found;
    private Action<string> _scanDone;
    private long _scanUntil;

    public DirectLobbyBackend(DirectTransport transport, Func<long> clock)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _scanner.Answered += OnAnswered;
    }

    public LobbyKind Kind => LobbyKind.Direct;

    public ITransport Transport => _transport;

    public string Unavailable => null;

    /// <summary>The first port a scan asks; it also asks the next <see cref="ScanPorts"/> - 1.</summary>
    public int BasePort { get; set; } = Session.Protocol.DefaultPort;

    /// <summary>How many ports a scan asks, so games hosted on the next few ports (a second copy) are found too.</summary>
    public int ScanPorts { get; set; } = 4;

    public int ScanDurationMs { get; set; } = 1500;

    /// <summary>How long a lookup waits for the host's answer before connecting without it.</summary>
    public int LookupTimeoutMs { get; set; } = 2500;

    /// <summary>The address codes and invites carry when set (a public IP); otherwise this machine's network address.</summary>
    public string PublicHost { get; set; }

    public event Action<LobbyAddress> JoinRequested
    {
        add { }
        remove { }
    }

    public void Open(LobbyInfo info, Action<LobbyAddress, string> done)
    {
        _reply = LobbyWire.Reply(info);
        _transport.Responder = Respond;
        done(LobbyAddress.Direct(InviteHost(), info.Port), null);
    }

    public void Update(LobbyInfo info)
    {
        if (_reply != null)
            _reply = LobbyWire.Reply(info);
    }

    public void Close()
    {
        _reply = null;
        _transport.Responder = null;
        _lookupDone = null;
    }

    public void Lookup(LobbyAddress address, Action<LobbyInfo, string, string> done)
    {
        string target = address.ToString();
        try
        {
            _lookupEndPoint = _scanner.Ask(address.Host, address.Port);
        }
        catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
        {
            done(null, null, e.Message);
            return;
        }
        _lookupAddress = address;
        _lookupDone = done;
        _lookupUntil = _clock() + LookupTimeoutMs;
    }

    public void Scan(LobbyAddress only, Action<LobbyListing> found, Action<string> done)
    {
        StopScan();
        try
        {
            if (only.Kind == LobbyKind.Direct)
                _scanner.Ask(only.Host, only.Port);
            else
                _scanner.Broadcast(BasePort, Math.Max(1, ScanPorts));
        }
        catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
        {
            done(e.Message);
            return;
        }
        _found = found;
        _scanDone = done;
        _scanUntil = _clock() + ScanDurationMs;
    }

    public void StopScan()
    {
        _found = null;
        _scanDone = null;
    }

    public bool ShowInviteDialog() => false;

    public bool Resume(LobbyAddress address, LobbyInfo info, bool hosting)
    {
        if (hosting && info != null)
        {
            _reply = LobbyWire.Reply(info);
            _transport.Responder = Respond;
        }
        return true;
    }

    public void Poll(long now)
    {
        _scanner.Poll();
        if (_lookupDone != null && now >= _lookupUntil)
        {
            // No answer: maybe an older ObenCoop or a filter on the way. The handshake still decides.
            var done = _lookupDone;
            _lookupDone = null;
            done(null, _lookupAddress.ToString(), null);
        }
        if (_scanDone != null && now >= _scanUntil)
        {
            var done = _scanDone;
            _found = null;
            _scanDone = null;
            done(null);
        }
        StopIfIdle();
    }

    public void Dispose()
    {
        _scanner.Answered -= OnAnswered;
        _scanner.Dispose();
        if (_transport.Responder != null && _reply != null)
            _transport.Responder = null;
    }

    /// <summary>
    /// The address put in codes: <see cref="PublicHost"/> when it is an IPv4 address, else this
    /// machine's address on its network (127.0.0.1 when there is none).
    /// </summary>
    public string InviteHost()
    {
        if (LobbyAddress.IsIPv4(PublicHost))
            return PublicHost;
        try
        {
            string local = NetUtils.GetLocalIp(LocalAddrType.IPv4);
            return LobbyAddress.IsIPv4(local) ? local : "127.0.0.1";
        }
        catch (Exception)
        {
            return "127.0.0.1";
        }
    }

    private byte[] Respond(byte[] data, int offset, int count) =>
        LobbyWire.IsQuery(data, offset, count) ? _reply : null;

    private void OnAnswered(IPEndPoint from, LobbyInfo info, int pingMs)
    {
        if (_lookupDone != null && _lookupEndPoint != null && from.Equals(_lookupEndPoint))
        {
            var done = _lookupDone;
            _lookupDone = null;
            done(info, _lookupAddress.ToString(), null);
        }
        _found?.Invoke(new LobbyListing(LobbyAddress.Direct(from.Address.ToString(), from.Port), info, pingMs));
    }

    // Only from the end of Poll: the socket is never closed from inside its own event loop.
    private void StopIfIdle()
    {
        if (_lookupDone == null && _scanDone == null && _scanner.IsRunning)
            _scanner.Stop();
    }
}
