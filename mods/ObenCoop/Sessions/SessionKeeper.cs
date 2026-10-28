using System;
using System.Threading;
using ObenCoop.Net.Transport;
using ObenCoop.Net.Transport.P2P;
using ObenCoop.Steam;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenCoop.Sessions;

/// <summary>
/// Carries a running session and its lobby over a hot reload of the mod. On unload the live
/// connection and the session and lobby as plain bytes go into ReloadState; the next copy of the mod
/// picks them up in OnLoad. A direct connection is a LiteNetLib socket (never reloaded); a Steam one is
/// just a list of who is who, since Steam keeps the sessions and the lobby itself. If nobody picks the
/// parcel up (the mod was switched off instead of reloaded) the connection is closed after a few
/// seconds, and a Steam lobby is left, so nothing stays open.
/// </summary>
internal static class SessionKeeper
{
    private const string ConnectionKey = "ObenCoop.Connection";
    private const string StateKey = "ObenCoop.SessionState";
    private const string LobbyKey = "ObenCoop.LobbyState";
    private const int ParkedLifetimeMs = 10000;

    /// <summary>Parks an active session; stops the transports when there is nothing to keep.</summary>
    public static void Park(CoopMod mod, ModLogger log)
    {
        var session = mod.Session;
        var steam = mod.Steam;
        byte[] state = session.IsActive ? session.SaveState() : null;
        byte[] lobby = state != null ? mod.Lobby.SaveState() : null;

        object[] connection = null;
        Action abandon = null;
        if (state != null && session.Transport == mod.Direct)
        {
            connection = mod.Direct.Detach();
            abandon = () => DirectTransport.Abandon(connection);
        }
        else if (state != null && steam.Transport != null && session.Transport == steam.Transport)
        {
            connection = steam.Transport.Detach();
            var network = steam.Network;
            ulong steamLobby = steam.LobbyId;
            abandon = () =>
            {
                P2PTransport.Abandon(connection, network);
                SteamLobbies.LeaveNow(steamLobby);
            };
        }

        if (connection == null)
        {
            mod.Direct.Stop();
            steam.Transport?.Stop();
            return;
        }

        ReloadState.Put(StateKey, state);
        ReloadState.Put(ConnectionKey, connection);
        if (lobby != null)
            ReloadState.Put(LobbyKey, lobby);
        log.Info("Parked the co-op session for the reload.");

        Timer timer = null;
        timer = new Timer(_ =>
        {
            timer?.Dispose();
            // ReloadState isn't thread safe, so look from the main thread.
            MainThread.Post(() => CloseIfStillParked(connection, abandon, log));
        }, null, ParkedLifetimeMs, Timeout.Infinite);
    }

    /// <summary>Takes over a parked session and lobby, if there are some. Returns true when they run again.</summary>
    public static bool TryResume(CoopMod mod, ModLogger log)
    {
        if (!ReloadState.Take(ConnectionKey, out object[] connection))
            return false;
        ReloadState.Take(StateKey, out byte[] state);
        ReloadState.Take(LobbyKey, out byte[] lobby);

        bool steam = P2PTransport.IsParked(connection);
        bool adopted = steam
            ? mod.Steam.TryStartNow() && mod.Steam.Transport.Adopt(connection)
            : mod.Direct.Adopt(connection);
        if (adopted)
            mod.Session.UseTransport(steam ? mod.Steam.Transport : (ITransport)mod.Direct);

        if (!adopted || !mod.Session.RestoreState(state))
        {
            log.Warn("A parked co-op connection couldn't be picked up; closing it.");
            if (steam)
            {
                mod.Steam.Transport?.Detach();
                P2PTransport.Abandon(connection, mod.Steam.Network);
            }
            else
            {
                mod.Direct.Stop();
                DirectTransport.Abandon(connection);
            }
            return false;
        }

        if (lobby != null && !mod.Lobby.RestoreState(lobby))
            log.Warn("The lobby couldn't be picked up after the reload; the game carries on without lobby features (codes, lists).");
        return true;
    }

    private static void CloseIfStillParked(object[] connection, Action abandon, ModLogger log)
    {
        if (ReloadState.Take(ConnectionKey, out object[] left) && ReferenceEquals(left, connection))
        {
            ReloadState.Take(StateKey, out byte[] _);
            ReloadState.Take(LobbyKey, out byte[] _);
            try { abandon(); }
            catch (Exception e) { log.Warn("Closing the parked connection failed: " + e.Message); }
            log.Info("Nothing picked up the parked co-op connection; closed it.");
        }
    }
}
