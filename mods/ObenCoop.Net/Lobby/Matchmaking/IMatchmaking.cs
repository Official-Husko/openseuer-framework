using System;
using System.Collections.Generic;

namespace ObenCoop.Net.Lobby.Matchmaking;

/// <summary>Who can see and join a platform lobby.</summary>
public enum LobbyVisibility
{
    /// <summary>Only people who are invited.</summary>
    Private,

    /// <summary>Friends see it (and the people they are in it with); it is not in public lists.</summary>
    FriendsOnly,

    /// <summary>Anyone who looks for ObenCoop games.</summary>
    Public,
}

/// <summary>
/// A platform's lobby service, shaped after Steam's <c>ISteamMatchmaking</c> with the friends and
/// rich presence bits a lobby needs. The mod implements it on Steamworks; the tests in memory.
/// <see cref="MatchmakingBackend"/> turns it into a <see cref="ILobbyBackend"/>. Callbacks and events
/// come from the platform's callback pump on the main thread.
/// </summary>
public interface IMatchmaking
{
    /// <summary>This player's id (a SteamID64), 0 when not signed in.</summary>
    ulong LocalId { get; }

    /// <summary>Calls <paramref name="done"/> with the new lobby's id, or 0 when it couldn't be made.</summary>
    void CreateLobby(LobbyVisibility visibility, int maxMembers, Action<ulong> done);

    /// <summary>Calls <paramref name="done"/> with true once this player is a member.</summary>
    void JoinLobby(ulong lobby, Action<bool> done);

    void LeaveLobby(ulong lobby);

    /// <summary>The lobby's owner, 0 when unknown (not a member and no data asked for yet).</summary>
    ulong Owner(ulong lobby);

    /// <summary>Fills <paramref name="into"/> with the members (cleared first). Only works for a lobby this player is in.</summary>
    void Members(ulong lobby, List<ulong> into);

    /// <summary>Sets a lobby data key; only the owner can.</summary>
    void SetData(ulong lobby, string key, string value);

    /// <summary>A lobby data value, empty when unknown.</summary>
    string GetData(ulong lobby, string key);

    /// <summary>Asks for a lobby's data without joining it; <see cref="LobbyChanged"/> says when it is there.</summary>
    void RequestData(ulong lobby);

    /// <summary>Public lobbies whose <paramref name="key"/> is <paramref name="value"/>. Their data is there when <paramref name="done"/> runs.</summary>
    void FindLobbies(string key, string value, Action<List<ulong>> done);

    /// <summary>Fills <paramref name="into"/> with lobbies that friends playing this game are in (cleared first).</summary>
    void FriendLobbies(List<ulong> into);

    /// <summary>Opens the platform's invite dialog for a lobby. False when there is no overlay.</summary>
    bool ShowInviteDialog(ulong lobby);

    /// <summary>Tells friends where to join this player ("Join game" in the friends list).</summary>
    void SetJoinTarget(ulong lobby);

    void ClearJoinTarget();

    /// <summary>Raised when a lobby's members or data changed.</summary>
    event Action<ulong> LobbyChanged;

    /// <summary>Raised when the player accepted an invite or clicked "Join game" on a friend, with the lobby id.</summary>
    event Action<ulong> JoinRequested;
}
