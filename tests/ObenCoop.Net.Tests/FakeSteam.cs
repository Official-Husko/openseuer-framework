using System;
using System.Collections.Generic;
using System.Linq;
using ObenCoop.Net.Lobby.Matchmaking;
using ObenCoop.Net.Transport.P2P;

namespace ObenCoop.Net.Tests;

/// <summary>
/// Steam in memory: users with peer-to-peer sessions (a session has to be accepted before packets
/// from a new sender come through, as with ISteamNetworking) and lobbies with owners, members and
/// data. Asynchronous answers (lobby created, entered, lists) wait for <see cref="RunCallbacks"/>,
/// like Steam's callback pump. Everyone is everyone's friend.
/// </summary>
internal sealed class FakeSteam
{
    private readonly List<Action> _callbacks = new List<Action>();
    private ulong _nextUser = 76561198000000001UL;
    private ulong _nextLobby = 109775240000000001UL;

    public long Now { get; set; } = 1000;
    public Dictionary<ulong, FakeUser> Users { get; } = new Dictionary<ulong, FakeUser>();
    public Dictionary<ulong, FakeLobby> Lobbies { get; } = new Dictionary<ulong, FakeLobby>();

    public FakeUser AddUser() => Users[_nextUser] = new FakeUser(this, _nextUser++);

    public void Later(Action action) => _callbacks.Add(action);

    public void RunCallbacks()
    {
        var due = _callbacks.ToArray();
        _callbacks.Clear();
        foreach (var action in due)
            action();
    }

    internal ulong NewLobby(FakeUser owner, LobbyVisibility visibility, int max)
    {
        var lobby = new FakeLobby { Id = _nextLobby++, Owner = owner.LocalId, Visibility = visibility, Max = max };
        lobby.Members.Add(owner.LocalId);
        Lobbies[lobby.Id] = lobby;
        return lobby.Id;
    }

    internal void Changed(FakeLobby lobby)
    {
        foreach (ulong member in lobby.Members.ToArray())
            Users[member].RaiseChanged(lobby.Id);
    }
}

internal sealed class FakeLobby
{
    public ulong Id;
    public ulong Owner;
    public LobbyVisibility Visibility;
    public int Max;
    public readonly List<ulong> Members = new List<ulong>();
    public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
}

internal sealed class FakeUser : IP2PNetwork, IMatchmaking
{
    private readonly FakeSteam _steam;
    private readonly Queue<(ulong From, byte[] Data)> _inbox = new Queue<(ulong, byte[])>();
    private readonly HashSet<ulong> _open = new HashSet<ulong>();
    private readonly Dictionary<ulong, List<byte[]>> _waiting = new Dictionary<ulong, List<byte[]>>();

    public FakeUser(FakeSteam steam, ulong id)
    {
        _steam = steam;
        LocalId = id;
    }

    public ulong LocalId { get; }
    public bool Offline { get; set; }
    public int InviteDialogs { get; private set; }
    public ulong JoinTarget { get; private set; }

    public event Action<ulong> SessionRequested;
    public event Action<ulong, string> SessionFailed;
    public event Action<ulong> LobbyChanged;
    public event Action<ulong> JoinRequested;

    // Peer to peer

    public bool Send(ulong to, byte[] data, int count, bool reliable)
    {
        if (Offline || !_steam.Users.TryGetValue(to, out var target) || target.Offline)
            return true;
        _open.Add(to);
        target.Deliver(LocalId, data.Take(count).ToArray());
        return true;
    }

    public int Receive(byte[] buffer, out ulong from)
    {
        from = 0;
        if (_inbox.Count == 0)
            return -1;
        var (sender, data) = _inbox.Dequeue();
        from = sender;
        if (data.Length > buffer.Length)
            return 0;
        Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
        return data.Length;
    }

    public void Accept(ulong peer)
    {
        _open.Add(peer);
        if (_waiting.TryGetValue(peer, out var held))
        {
            _waiting.Remove(peer);
            foreach (var packet in held)
                _inbox.Enqueue((peer, packet));
        }
    }

    public void Close(ulong peer)
    {
        _open.Remove(peer);
        _waiting.Remove(peer);
        Closed.Add(peer);
    }

    public List<ulong> Closed { get; } = new List<ulong>();

    public int QueuedPackets(ulong peer) => 0;

    public void Fail(ulong peer, string reason) => SessionFailed?.Invoke(peer, reason);

    private void Deliver(ulong from, byte[] data)
    {
        if (_open.Contains(from))
        {
            _inbox.Enqueue((from, data));
            return;
        }
        if (!_waiting.TryGetValue(from, out var held))
        {
            _waiting[from] = held = new List<byte[]>();
            SessionRequested?.Invoke(from);
        }
        held.Add(data);
    }

    // Matchmaking

    public void CreateLobby(LobbyVisibility visibility, int maxMembers, Action<ulong> done) =>
        _steam.Later(() => done(Offline ? 0 : _steam.NewLobby(this, visibility, maxMembers)));

    public void JoinLobby(ulong lobby, Action<bool> done) => _steam.Later(() =>
    {
        if (!_steam.Lobbies.TryGetValue(lobby, out var room) || room.Members.Count >= room.Max)
        {
            done(false);
            return;
        }
        if (!room.Members.Contains(LocalId))
            room.Members.Add(LocalId);
        done(true);
        _steam.Changed(room);
    });

    public void LeaveLobby(ulong lobby)
    {
        if (!_steam.Lobbies.TryGetValue(lobby, out var room) || !room.Members.Remove(LocalId))
            return;
        if (room.Members.Count == 0)
        {
            _steam.Lobbies.Remove(lobby);
            return;
        }
        if (room.Owner == LocalId)
            room.Owner = room.Members[0];
        _steam.Changed(room);
    }

    public ulong Owner(ulong lobby) => _steam.Lobbies.TryGetValue(lobby, out var room) ? room.Owner : 0;

    public void Members(ulong lobby, List<ulong> into)
    {
        into.Clear();
        if (_steam.Lobbies.TryGetValue(lobby, out var room) && room.Members.Contains(LocalId))
            into.AddRange(room.Members);
    }

    public void SetData(ulong lobby, string key, string value)
    {
        if (_steam.Lobbies.TryGetValue(lobby, out var room) && room.Owner == LocalId)
        {
            room.Data[key] = value;
            _steam.Changed(room);
        }
    }

    public string GetData(ulong lobby, string key) =>
        _steam.Lobbies.TryGetValue(lobby, out var room) && room.Data.TryGetValue(key, out var value) ? value : "";

    public void RequestData(ulong lobby) => _steam.Later(() => RaiseChanged(lobby));

    public void FindLobbies(string key, string value, Action<List<ulong>> done) => _steam.Later(() =>
        done(_steam.Lobbies.Values.Where(l => l.Visibility == LobbyVisibility.Public && GetData(l.Id, key) == value).Select(l => l.Id).ToList()));

    public void FriendLobbies(List<ulong> into)
    {
        into.Clear();
        into.AddRange(_steam.Lobbies.Values.Where(l => l.Visibility != LobbyVisibility.Private && !l.Members.Contains(LocalId)).Select(l => l.Id));
    }

    public bool ShowInviteDialog(ulong lobby)
    {
        InviteDialogs++;
        return true;
    }

    public void SetJoinTarget(ulong lobby) => JoinTarget = lobby;

    public void ClearJoinTarget() => JoinTarget = 0;

    public void Invited(ulong lobby) => JoinRequested?.Invoke(lobby);

    internal void RaiseChanged(ulong lobby) => LobbyChanged?.Invoke(lobby);
}
