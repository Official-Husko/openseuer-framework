// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenCoop.Net.Transport.P2P;
using Steamworks;

namespace ObenCoop.Steam;

/// <summary>
/// Steam's legacy peer-to-peer networking (ISteamNetworking, the API the game's Steamworks.NET has and
/// the reference framework used) as an <see cref="IP2PNetwork"/>. Every call is a thin wrapper, so
/// moving to SteamNetworkingSockets later touches only this file. Only made once Steam is up.
/// </summary>
internal sealed class SteamP2P : IP2PNetwork, IDisposable
{
    /// <summary>The P2P channel ObenCoop uses. Any number works; this one is unlikely to meet another mod's.</summary>
    public const int Channel = 2789;

    private Callback<P2PSessionRequest_t> _requested;
    private Callback<P2PSessionConnectFail_t> _failed;
    private byte[] _oversize = new byte[0];

    public SteamP2P()
    {
        _requested = Callback<P2PSessionRequest_t>.Create(r => SessionRequested?.Invoke(r.m_steamIDRemote.m_SteamID));
        _failed = Callback<P2PSessionConnectFail_t>.Create(f => SessionFailed?.Invoke(f.m_steamIDRemote.m_SteamID, Describe(f.m_eP2PSessionError)));
    }

    public ulong LocalId => SteamUser.GetSteamID().m_SteamID;

    public event Action<ulong> SessionRequested;
    public event Action<ulong, string> SessionFailed;

    public bool Send(ulong to, byte[] data, int count, bool reliable) =>
        SteamNetworking.SendP2PPacket(new CSteamID(to), data, (uint)count, reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliable, Channel);

    public int Receive(byte[] buffer, out ulong from)
    {
        from = 0;
        if (!SteamNetworking.IsP2PPacketAvailable(out uint size, Channel))
            return -1;

        if (size > buffer.Length)
        {
            // Bigger than anything ObenCoop sends: read it out of the way and report it as dropped.
            if (_oversize.Length < size)
                _oversize = new byte[size];
            SteamNetworking.ReadP2PPacket(_oversize, size, out _, out var sender, Channel);
            from = sender.m_SteamID;
            return 0;
        }

        if (!SteamNetworking.ReadP2PPacket(buffer, (uint)buffer.Length, out uint read, out var remote, Channel))
            return -1;
        from = remote.m_SteamID;
        return (int)read;
    }

    public void Accept(ulong peer) => SteamNetworking.AcceptP2PSessionWithUser(new CSteamID(peer));

    public void Close(ulong peer) => SteamNetworking.CloseP2PSessionWithUser(new CSteamID(peer));

    public int QueuedPackets(ulong peer) =>
        SteamNetworking.GetP2PSessionState(new CSteamID(peer), out var state) ? state.m_nPacketsQueuedForSend : 0;

    public void Dispose()
    {
        _requested?.Dispose();
        _failed?.Dispose();
        _requested = null;
        _failed = null;
    }

    private static string Describe(byte error) => (EP2PSessionError)error switch
    {
        EP2PSessionError.k_EP2PSessionErrorNotRunningApp => "The other player isn't running the game.",
        EP2PSessionError.k_EP2PSessionErrorNoRightsToApp => "The other player doesn't own the game.",
        EP2PSessionError.k_EP2PSessionErrorDestinationNotLoggedIn => "The other player isn't signed in to Steam.",
        EP2PSessionError.k_EP2PSessionErrorTimeout => "Steam couldn't reach the other player.",
        _ => "The Steam connection failed.",
    };
}
