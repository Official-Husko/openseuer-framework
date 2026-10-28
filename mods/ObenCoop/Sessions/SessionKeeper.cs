using System.Threading;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenCoop.Sessions;

/// <summary>
/// Carries a running session over a hot reload of the mod. On unload the live UDP connection (LiteNetLib
/// objects, which are never reloaded) and the session as plain bytes go into ReloadState; the next copy
/// of the mod picks both up in OnLoad. If nobody does (the mod was switched off instead of reloaded) the
/// connection is closed after a few seconds so no socket is left open.
/// </summary>
internal static class SessionKeeper
{
    private const string ConnectionKey = "ObenCoop.Connection";
    private const string StateKey = "ObenCoop.SessionState";
    private const int ParkedLifetimeMs = 10000;

    /// <summary>Parks an active session; closes the transport if there is nothing to keep.</summary>
    public static void Park(CoopSession session, DirectTransport transport, ModLogger log)
    {
        byte[] state = session.IsActive ? session.SaveState() : null;
        object[] connection = state != null ? transport.Detach() : null;
        if (connection == null)
        {
            transport.Stop();
            return;
        }

        ReloadState.Put(StateKey, state);
        ReloadState.Put(ConnectionKey, connection);
        log.Info("Parked the co-op session for the reload.");

        Timer timer = null;
        timer = new Timer(_ =>
        {
            timer?.Dispose();
            // ReloadState isn't thread safe, so look from the main thread.
            MainThread.Post(() => CloseIfStillParked(connection, log));
        }, null, ParkedLifetimeMs, Timeout.Infinite);
    }

    /// <summary>Takes over a parked session, if there is one. Returns true when it is running again.</summary>
    public static bool TryResume(CoopSession session, DirectTransport transport, ModLogger log)
    {
        if (!ReloadState.Take(ConnectionKey, out object[] connection))
            return false;
        ReloadState.Take(StateKey, out byte[] state);

        if (!transport.Adopt(connection) || !session.RestoreState(state))
        {
            log.Warn("A parked co-op connection couldn't be picked up; closing it.");
            DirectTransport.Abandon(connection);
            transport.Stop();
            return false;
        }
        return true;
    }

    private static void CloseIfStillParked(object[] connection, ModLogger log)
    {
        if (ReloadState.Take(ConnectionKey, out object[] left) && ReferenceEquals(left, connection))
        {
            ReloadState.Take(StateKey, out byte[] _);
            DirectTransport.Abandon(left);
            log.Info("Nothing picked up the parked co-op connection; closed it.");
        }
    }
}
