// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using ObenCoop.Net.Lobby.Matchmaking;
using Steamworks;

namespace ObenCoop.Steam;

/// <summary>
/// Steam lobbies, friends and rich presence as an <see cref="IMatchmaking"/>. Thin wrappers over
/// Steamworks.NET; the logic is in MatchmakingBackend. Only made once Steam is up; dispose it to
/// unregister the callbacks (on unload), which does not leave any lobby.
/// </summary>
internal sealed class SteamLobbies : IMatchmaking, IDisposable
{
    /// <summary>What Steam puts on the command line or in rich presence to join a lobby.</summary>
    public const string ConnectPrefix = "+connect_lobby";

    private readonly List<IDisposable> _callbacks = new List<IDisposable>();
    private CallResult<LobbyCreated_t> _created;
    private CallResult<LobbyEnter_t> _entered;
    private CallResult<LobbyMatchList_t> _listed;

    private readonly uint _appId;

    public SteamLobbies()
    {
        _appId = SteamUtils.GetAppID().m_AppId;
        _callbacks.Add(Callback<LobbyChatUpdate_t>.Create(u => LobbyChanged?.Invoke(u.m_ulSteamIDLobby)));
        _callbacks.Add(Callback<LobbyDataUpdate_t>.Create(u => LobbyChanged?.Invoke(u.m_ulSteamIDLobby)));
        _callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(r => JoinRequested?.Invoke(r.m_steamIDLobby.m_SteamID)));
        _callbacks.Add(Callback<GameRichPresenceJoinRequested_t>.Create(r => RaiseConnect(r.m_rgchConnect)));
        _created = CallResult<LobbyCreated_t>.Create();
        _entered = CallResult<LobbyEnter_t>.Create();
        _listed = CallResult<LobbyMatchList_t>.Create();
        _callbacks.Add(_created);
        _callbacks.Add(_entered);
        _callbacks.Add(_listed);
    }

    public ulong LocalId => SteamUser.GetSteamID().m_SteamID;

    public event Action<ulong> LobbyChanged;
    public event Action<ulong> JoinRequested;

    public void CreateLobby(LobbyVisibility visibility, int maxMembers, Action<ulong> done)
    {
        var call = SteamMatchmaking.CreateLobby(ToSteam(visibility), maxMembers);
        _created.Set(call, (result, failed) => done(!failed && result.m_eResult == EResult.k_EResultOK ? result.m_ulSteamIDLobby : 0));
    }

    public void JoinLobby(ulong lobby, Action<bool> done)
    {
        var call = SteamMatchmaking.JoinLobby(new CSteamID(lobby));
        _entered.Set(call, (result, failed) =>
            done(!failed && result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess));
    }

    public void LeaveLobby(ulong lobby) => SteamMatchmaking.LeaveLobby(new CSteamID(lobby));

    public ulong Owner(ulong lobby) => SteamMatchmaking.GetLobbyOwner(new CSteamID(lobby)).m_SteamID;

    public void Members(ulong lobby, List<ulong> into)
    {
        into.Clear();
        var id = new CSteamID(lobby);
        int count = SteamMatchmaking.GetNumLobbyMembers(id);
        for (int i = 0; i < count; i++)
            into.Add(SteamMatchmaking.GetLobbyMemberByIndex(id, i).m_SteamID);
    }

    public void SetData(ulong lobby, string key, string value) => SteamMatchmaking.SetLobbyData(new CSteamID(lobby), key, value);

    public string GetData(ulong lobby, string key) => SteamMatchmaking.GetLobbyData(new CSteamID(lobby), key) ?? "";

    public void RequestData(ulong lobby) => SteamMatchmaking.RequestLobbyData(new CSteamID(lobby));

    public void FindLobbies(string key, string value, Action<List<ulong>> done)
    {
        SteamMatchmaking.AddRequestLobbyListStringFilter(key, value, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        var call = SteamMatchmaking.RequestLobbyList();
        _listed.Set(call, (result, failed) =>
        {
            var lobbies = new List<ulong>();
            for (int i = 0; !failed && i < result.m_nLobbiesMatching; i++)
                lobbies.Add(SteamMatchmaking.GetLobbyByIndex(i).m_SteamID);
            done(lobbies);
        });
    }

    public void FriendLobbies(List<ulong> into)
    {
        into.Clear();
        int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        for (int i = 0; i < count; i++)
        {
            var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            // Friends in the same app as this game: Obenseuer, or Spacewar when the game started Steam as it.
            if (!SteamFriends.GetFriendGamePlayed(friend, out var game) || game.m_gameID.AppID().m_AppId != _appId)
                continue;
            if (game.m_steamIDLobby.IsValid() && game.m_steamIDLobby.IsLobby() && !into.Contains(game.m_steamIDLobby.m_SteamID))
                into.Add(game.m_steamIDLobby.m_SteamID);
        }
    }

    public bool ShowInviteDialog(ulong lobby)
    {
        if (!SteamUtils.IsOverlayEnabled())
            return false;
        SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobby));
        return true;
    }

    public void SetJoinTarget(ulong lobby) =>
        SteamFriends.SetRichPresence("connect", ConnectPrefix + " " + lobby.ToString(CultureInfo.InvariantCulture));

    public void ClearJoinTarget() => SteamFriends.ClearRichPresence();

    /// <summary>Raises <see cref="JoinRequested"/> for a "+connect_lobby &lt;id&gt;" text. False when it isn't one.</summary>
    public bool RaiseConnect(string text)
    {
        if (!TryParseConnect(text, out ulong lobby))
            return false;
        JoinRequested?.Invoke(lobby);
        return true;
    }

    public static bool TryParseConnect(string text, out ulong lobby)
    {
        lobby = 0;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || !text.StartsWith(ConnectPrefix, StringComparison.Ordinal))
            return false;
        return ulong.TryParse(text.Substring(ConnectPrefix.Length).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out lobby) && lobby != 0;
    }

    /// <summary>Leaves a lobby without an instance (closing a session parked for a reload that never came).</summary>
    public static void LeaveNow(ulong lobby)
    {
        if (lobby == 0)
            return;
        SteamMatchmaking.LeaveLobby(new CSteamID(lobby));
        SteamFriends.ClearRichPresence();
    }

    public void Dispose()
    {
        foreach (var callback in _callbacks)
            callback.Dispose();
        _callbacks.Clear();
    }

    private static ELobbyType ToSteam(LobbyVisibility visibility) => visibility switch
    {
        LobbyVisibility.Public => ELobbyType.k_ELobbyTypePublic,
        LobbyVisibility.Private => ELobbyType.k_ELobbyTypePrivate,
        _ => ELobbyType.k_ELobbyTypeFriendsOnly,
    };
}
