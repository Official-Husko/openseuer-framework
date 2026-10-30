using System;
using HarmonyLib;
using ObenFramework.Patching;
using ObenLoader;
using ObenLoader.Runtime;
using Steamworks;

namespace ObenFramework.Game;

// Starting Steam as another app: the game runs as Valve's Spacewar test app (480) instead of Obenseuer.
public static partial class GameSteam
{
    /// <summary>
    /// Valve's Spacewar test app. Every Steam account has it, so lobbies and peer-to-peer networking
    /// work for copies that don't own the game on Steam (GOG and other stores), and testing doesn't need
    /// a second copy of the game. While the game runs as Spacewar, Obenseuer's achievements, stats and
    /// play time don't reach Steam, and friends see the player in Spacewar.
    /// </summary>
    public const uint SpacewarAppId = 480;

    private static uint _startAs;
    private static string _startAsBy;

    /// <summary>The app id <see cref="StartAs"/> set for this run of the game, 0 when none.</summary>
    public static uint RequestedAppId => _startAs;

    /// <summary>
    /// Has the game start Steam as another app (<see cref="SpacewarAppId"/>, or <see cref="GameAppId"/> to take
    /// a request back). It only works before the game starts Steam, so call it from <c>OnLoad</c>: mods
    /// load before the first scene, and the game starts Steam in it. Later the switch needs a restart.
    /// The game's own relaunch through Steam as Obenseuer is skipped for a requested app.
    /// </summary>
    /// <param name="owner">Who asked, for the log.</param>
    /// <param name="appId">The app to start as.</param>
    /// <param name="problem">Why it didn't work, or null.</param>
    /// <returns>False when the game has started Steam already.</returns>
    public static bool StartAs(ITracker owner, uint appId, out string problem)
    {
        problem = null;
        if (appId == 0)
            throw new ArgumentOutOfRangeException(nameof(appId), "0 isn't a Steam app.");
        if (SteamStarted())
        {
            uint running = AppId;
            if (running == appId)
                return true;
            problem = running != 0
                ? $"The game started Steam as app {running} already; the switch to {appId} needs a restart of the game."
                : "The game tried to start Steam already; a switch of the Steam app needs a restart of the game.";
            return false;
        }

        string text = appId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Environment.SetEnvironmentVariable("SteamAppId", text);
        Environment.SetEnvironmentVariable("SteamGameId", text);
        _startAs = appId == GameAppId ? 0 : appId;
        _startAsBy = owner is ObenMod mod ? mod.Info.Id : owner?.GetType().Name;
        FrameworkMod.Instance?.Log.Info(_startAs != 0
            ? $"Steam will start as app {appId} for this run of the game (asked by {_startAsBy ?? "a mod"})."
            : "Steam will start as Obenseuer.");
        return true;
    }

    // The game's SteamManager woke up: Steam started, or failed to.
    private static bool SteamStarted()
    {
        try
        {
            EnsureFields();
            return _manager() != null || IsReady;
        }
        catch (Exception)
        {
            return IsReady;
        }
    }

    /// <summary>Keeps the game from relaunching itself through Steam as Obenseuer when another app was asked for.</summary>
    [HarmonyPatch(typeof(SteamAPI), nameof(SteamAPI.RestartAppIfNecessary))]
    [PatchArea("Steam")]
    internal static class RestartPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (_startAs == 0)
                return true;
            __result = false;
            return false;
        }
    }
}
