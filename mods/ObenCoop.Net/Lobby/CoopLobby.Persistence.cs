using System;
using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Lobby;

// Carrying an open lobby over a hot reload of the mod, next to the session (CoopSession.SaveState).
// Only Hosting and Joined are kept: a request still running is simply dropped with its session.
public sealed partial class CoopLobby
{
    private const byte StateFormat = 1;

    /// <summary>The open lobby as bytes, or null when there is none to keep.</summary>
    public byte[] SaveState()
    {
        if ((Status != LobbyStatus.Hosting && Status != LobbyStatus.Joined) || _active == null)
            return null;

        var writer = new MessageWriter();
        writer.WriteByte(StateFormat);
        writer.WriteByte((byte)Status);
        writer.WriteByte((byte)Address.Kind);
        writer.WriteString(Address.Host);
        writer.WriteInt32(Address.Port);
        writer.WriteInt64((long)Address.LobbyId);
        writer.WriteBool(Info != null);
        Info?.Write(writer);

        var bytes = new byte[writer.Length];
        Buffer.BlockCopy(writer.Buffer, 0, bytes, 0, writer.Length);
        return bytes;
    }

    /// <summary>
    /// Takes the lobby back from <see cref="SaveState"/> bytes, after the session was restored. False,
    /// leaving the lobby offline, when the bytes don't fit or the backend can't pick its lobby up.
    /// </summary>
    public bool RestoreState(byte[] state)
    {
        if (state == null || Status != LobbyStatus.Offline)
            return false;

        var reader = new MessageReader().Init(state, 0, state.Length);
        try
        {
            if (reader.ReadByte() != StateFormat)
                return false;
            var status = (LobbyStatus)reader.ReadByte();
            var kind = (LobbyKind)reader.ReadByte();
            string host = reader.ReadString();
            int port = reader.ReadInt32();
            ulong lobbyId = (ulong)reader.ReadInt64();
            LobbyInfo info = null;
            if (reader.ReadBool() && !LobbyInfo.TryRead(reader, out info))
                return false;

            var backend = Backend(kind);
            if (backend == null || (status != LobbyStatus.Hosting && status != LobbyStatus.Joined))
                return false;
            var address = kind == LobbyKind.Steam ? LobbyAddress.Steam(lobbyId) : LobbyAddress.Direct(host, port);
            if (status == LobbyStatus.Hosting && info != null)
                info.Players = Session.Roster.Count;
            if (!backend.Resume(address, info, status == LobbyStatus.Hosting))
                return false;

            _active = backend;
            _joining = status == LobbyStatus.Joined;
            Address = address;
            Info = info;
            Error = "";
            Status = status;
            Session.Log($"Carried the lobby over a reload ({status} at {address}).");
            return true;
        }
        catch (MessageFormatException)
        {
            return false;
        }
    }
}
