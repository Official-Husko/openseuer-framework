// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenCoop.Steam;

/// <summary>
/// Says whether the game's own Steam is up, without touching Steam. The game's SteamManager starts
/// the Steam API when it wakes; until it did (or when it failed, or there is no Steam at all) nothing
/// of the Steam transport may run. The public <c>SteamManager.Initialized</c> is not used: when no
/// manager exists it makes one, which starts Steam itself (or throws if Steam was started once
/// already). The private fields are read instead, at most once a second until the answer is yes.
/// </summary>
internal static class SteamGate
{
    private const int ProbeIntervalMs = 1000;

    private static AccessTools.FieldRef<SteamManager> _instance;
    private static AccessTools.FieldRef<SteamManager, bool> _initialized;
    private static bool _ready;
    private static bool _broken;
    private static bool _probed;
    private static int _nextProbe;

    /// <summary>True once the game's SteamManager has started the Steam API. Stays true.</summary>
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

    /// <summary>Why the gate can't tell, or null.</summary>
    public static string Problem { get; private set; }

    private static bool Probe()
    {
        try
        {
            if (_instance == null)
            {
                _instance = AccessTools.StaticFieldRefAccess<SteamManager>(AccessTools.Field(typeof(SteamManager), "s_instance"));
                _initialized = AccessTools.FieldRefAccess<SteamManager, bool>("m_bInitialized");
            }
            var manager = _instance();
            return manager != null && _initialized(manager);
        }
        catch (Exception e)
        {
            // The game changed its SteamManager; Steam play stays off rather than guessing.
            _broken = true;
            Problem = "The game's SteamManager couldn't be read: " + e.Message;
            return false;
        }
    }
}
