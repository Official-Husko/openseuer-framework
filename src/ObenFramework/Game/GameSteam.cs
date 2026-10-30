// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Steamworks;

namespace ObenFramework.Game;

/// <summary>
/// The game's own Steam: whether it is up, who is signed in, names and friends. Nothing here starts
/// Steam or touches it before the game did: until the game's SteamManager has started the Steam API
/// (or when it failed, or there is no Steam) every member answers "nothing" (false, 0, "" or an empty
/// list). Names come from other people, so they arrive without rich text tags, control characters or
/// invisible formatting characters.
/// </summary>
public static partial class GameSteam
{
    /// <summary>Obenseuer's Steam app id.</summary>
    public const uint GameAppId = 951240;

    /// <summary>Longest name handed out; Steam allows 32 characters.</summary>
    public const int MaxNameLength = 32;

    private const int ProbeIntervalMs = 1000;

    private static AccessTools.FieldRef<SteamManager> _manager;
    private static AccessTools.FieldRef<SteamManager, bool> _initialized;
    private static bool _ready;
    private static bool _broken;
    private static bool _probed;
    private static int _nextProbe;

    /// <summary>
    /// True once the game's SteamManager has started the Steam API, and from then on. Asks the game at
    /// most once a second until then, so it is cheap to read every frame.
    /// </summary>
    public static bool IsReady
    {
        get
        {
            if (_ready || _broken)
                return _ready;
            int now = Environment.TickCount;
            if (_probed && now - _nextProbe < 0)
                return false;
            _probed = true;
            _nextProbe = now + ProbeIntervalMs;
            _ready = Probe();
            return _ready;
        }
    }

    /// <summary>Why Steam can't be told apart from "not up yet" (the game changed its SteamManager), or null.</summary>
    public static string Problem { get; private set; }

    /// <summary>The signed-in player's Steam id, 0 when Steam isn't up.</summary>
    public static ulong LocalId => IsReady ? SteamUser.GetSteamID().m_SteamID : 0UL;

    /// <summary>The signed-in player's Steam name, "" when Steam isn't up.</summary>
    public static string LocalName => IsReady ? Clean(SteamFriends.GetPersonaName()) : "";

    /// <summary>The Steam overlay can open (invite dialogs, profiles).</summary>
    public static bool OverlayEnabled => IsReady && SteamUtils.IsOverlayEnabled();

    /// <summary>The app id Steam started the game as (<see cref="GameAppId"/> normally), 0 when Steam isn't up.</summary>
    public static uint AppId => IsReady ? SteamUtils.GetAppID().m_AppId : 0;

    /// <summary>
    /// Someone's Steam name. Steam only knows the names of friends and of people met recently (in a lobby,
    /// a game); for anyone else this asks Steam to fetch it and returns "", so ask again a moment later.
    /// </summary>
    public static string Name(ulong steamId)
    {
        if (steamId == 0 || !IsReady)
            return "";
        if (steamId == SteamUser.GetSteamID().m_SteamID)
            return LocalName;
        var id = new CSteamID(steamId);
        string name = SteamFriends.GetFriendPersonaName(id) ?? "";
        if (name.Length == 0 || name == "[unknown]")
        {
            SteamFriends.RequestUserInformation(id, true);
            return "";
        }
        return Clean(name);
    }

    /// <summary>The Steam ids of friends playing a game right now: this one unless <paramref name="appId"/> names another.</summary>
    public static List<ulong> FriendsPlaying(uint appId = GameAppId)
    {
        var playing = new List<ulong>();
        if (!IsReady)
            return playing;
        int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
        for (int i = 0; i < count; i++)
        {
            var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            if (SteamFriends.GetFriendGamePlayed(friend, out var game) && game.m_gameID.AppID().m_AppId == appId)
                playing.Add(friend.m_SteamID);
        }
        return playing;
    }

    private static bool Probe()
    {
        try
        {
            EnsureFields();
            var manager = _manager();
            return manager != null && _initialized(manager);
        }
        catch (Exception e)
        {
            _broken = true;
            Problem = "The game's SteamManager couldn't be read: " + e.Message;
            return false;
        }
    }

    // The public SteamManager.Initialized makes a manager when there is none, which starts Steam itself
    // (or throws when it ran once already), so the private fields are read instead.
    private static void EnsureFields()
    {
        if (_manager != null)
            return;
        _manager = AccessTools.StaticFieldRefAccess<SteamManager>(AccessTools.Field(typeof(SteamManager), "s_instance"));
        _initialized = AccessTools.FieldRefAccess<SteamManager, bool>("m_bInitialized");
    }

    // Names end up in rich text: no tags, no control characters, nothing that reorders or hides text.
    internal static string Clean(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        var text = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                text.Append(c).Append(name[++i]);
                continue;
            }
            if (c == '<' || c == '>' || char.IsControl(c) || char.IsSurrogate(c) || IsHidden(c))
                continue;
            text.Append(c);
        }
        string clean = text.ToString().Trim();
        if (clean.Length <= MaxNameLength)
            return clean;
        int cut = char.IsHighSurrogate(clean[MaxNameLength - 1]) ? MaxNameLength - 1 : MaxNameLength;
        return clean.Substring(0, cut);
    }

    // Right-to-left and other direction marks, zero-width characters, the soft hyphen and the BOM.
    private static bool IsHidden(char c) =>
        c == '\u061C' || c == '\u00AD' || c == '\u2060' || c == '\uFEFF' || (c >= '\u200B' && c <= '\u200F')
        || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069');
}
