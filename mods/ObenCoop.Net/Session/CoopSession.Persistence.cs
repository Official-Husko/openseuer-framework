using System;
using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Session;

// Carrying a session over a hot reload. The mod keeps the transport's connection (DirectTransport.Detach)
// and these bytes; the next copy of the mod adopts the connection into a new transport and restores
// the session on top of it. The bytes are plain data, so they don't depend on the old assembly.
public sealed partial class CoopSession
{
    private const byte StateFormat = 1;

    /// <summary>The session as bytes, or null when offline. Does not stop anything.</summary>
    public byte[] SaveState()
    {
        if (State == SessionState.Offline)
            return null;

        var writer = new MessageWriter();
        writer.WriteByte(StateFormat);
        writer.WriteByte((byte)State);
        writer.WriteInt32(_hostPeer);
        writer.WriteBool(_transportConnected);
        writer.WriteByte((byte)Roster.Count);
        foreach (var player in Roster.Players)
        {
            writer.WriteByte(player.Slot);
            writer.WriteGuid(player.Id);
            writer.WriteString(player.Name);
            writer.WriteBool(player.IsLocal);
            writer.WriteInt32(player.Peer);
        }

        var bytes = new byte[writer.Length];
        Buffer.BlockCopy(writer.Buffer, 0, bytes, 0, writer.Length);
        return bytes;
    }

    /// <summary>
    /// Picks the session up again from <see cref="SaveState"/> bytes. The transport must already be
    /// running (adopted). Returns false, leaving the session offline, when the bytes don't fit.
    /// No events are raised: the session simply carries on.
    /// </summary>
    public bool RestoreState(byte[] state)
    {
        if (state == null || State != SessionState.Offline || !_transport.IsRunning)
            return false;

        var reader = MessagePool.RentReader().Init(state, 0, state.Length);
        try
        {
            if (reader.ReadByte() != StateFormat)
                return false;
            var restored = (SessionState)reader.ReadByte();
            int hostPeer = reader.ReadInt32();
            bool connected = reader.ReadBool();
            int count = reader.ReadByte();

            Roster.Clear();
            for (int i = 0; i < count; i++)
            {
                byte slot = reader.ReadByte();
                var id = reader.ReadGuid();
                string name = reader.ReadString();
                bool local = reader.ReadBool();
                int peer = reader.ReadInt32();
                Roster.Add(new Player(slot, id, name, local, peer));
            }

            State = restored;
            _hostPeer = hostPeer;
            _transportConnected = connected;
            _helloSentAt = Options.Clock();
            Log($"Carried the session over a reload ({State}, {Roster.Count} player(s)).");
            return true;
        }
        catch (MessageFormatException)
        {
            Roster.Clear();
            State = SessionState.Offline;
            return false;
        }
        finally
        {
            MessagePool.Return(reader);
        }
    }
}
