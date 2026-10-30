using System;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// What a hosted game tells people who look for it: its name, who hosts it, how full it is, and what
/// a joiner must match (protocol, game version, mods). Sent as the answer to a lobby query on the
/// local network and stored in a Steam lobby's data. The format starts with its own version and the
/// protocol version, so a game with a newer format can still tell "different version" apart from junk.
/// </summary>
public sealed class LobbyInfo
{
    /// <summary>Longest lobby name in characters.</summary>
    public const int MaxNameLength = 32;

    private const byte Format = 1;
    private const int MaxVersionLength = 64;

    /// <summary>Random per hosted lobby, so a list can tell two answers from the same game apart from two games.</summary>
    public long Id { get; set; }

    /// <summary>The host's <see cref="Session.Protocol.Version"/>.</summary>
    public int Protocol { get; set; } = Session.Protocol.Version;

    public string GameVersion { get; set; } = "";

    /// <summary>The host's <see cref="ModSet.Fingerprint"/>.</summary>
    public uint ModsFingerprint { get; set; }

    public int ModCount { get; set; }

    public string Name { get; set; } = "";

    public string HostName { get; set; } = "";

    public int Players { get; set; }

    public int MaxPlayers { get; set; }

    /// <summary>False when the host doesn't take new players right now.</summary>
    public bool Open { get; set; } = true;

    /// <summary>The port the game listens on (direct play).</summary>
    public int Port { get; set; }

    /// <summary>True when the answer came in a format newer than this game reads: only <see cref="Protocol"/> is set.</summary>
    public bool Unreadable { get; private set; }

    public bool IsFull => MaxPlayers > 0 && Players >= MaxPlayers;

    /// <summary>The name to show, falling back to the host's name.</summary>
    public string DisplayName => Name.Length > 0 ? Name : (HostName.Length > 0 ? HostName + "'s game" : "Co-op game");

    public LobbyInfo Clone() => (LobbyInfo)MemberwiseClone();

    public void Write(MessageWriter writer)
    {
        writer.WriteByte(Format);
        writer.WriteInt32(Protocol);
        writer.WriteInt64(Id);
        writer.WriteString(Cut(GameVersion, MaxVersionLength));
        writer.WriteUInt32(ModsFingerprint);
        writer.WriteByte((byte)Clamp(ModCount));
        writer.WriteString(Cut(Name, MaxNameLength));
        writer.WriteString(Cut(HostName, Session.Protocol.MaxNameLength));
        writer.WriteByte((byte)Clamp(Players));
        writer.WriteByte((byte)Clamp(MaxPlayers));
        writer.WriteBool(Open);
        writer.WriteUInt16((ushort)Port);
    }

    /// <summary>Reads what <see cref="Write"/> wrote. False when the bytes are damaged.</summary>
    public static bool TryRead(MessageReader reader, out LobbyInfo info)
    {
        info = null;
        try
        {
            byte format = reader.ReadByte();
            int protocol = reader.ReadInt32();
            if (format != Format)
            {
                info = new LobbyInfo { Protocol = protocol, Unreadable = true };
                return true;
            }
            info = new LobbyInfo
            {
                Protocol = protocol,
                Id = reader.ReadInt64(),
                GameVersion = Cut(reader.ReadString(), MaxVersionLength),
                ModsFingerprint = reader.ReadUInt32(),
                ModCount = reader.ReadByte(),
                Name = Clean(reader.ReadString(), MaxNameLength),
                HostName = Clean(reader.ReadString(), Session.Protocol.MaxNameLength),
                Players = reader.ReadByte(),
                MaxPlayers = reader.ReadByte(),
                Open = reader.ReadBool(),
                Port = reader.ReadUInt16(),
            };
            return true;
        }
        catch (MessageFormatException)
        {
            info = null;
            return false;
        }
    }

    public byte[] ToBytes()
    {
        var writer = new MessageWriter(128);
        Write(writer);
        var bytes = new byte[writer.Length];
        Buffer.BlockCopy(writer.Buffer, 0, bytes, 0, writer.Length);
        return bytes;
    }

    public static bool TryFromBytes(byte[] bytes, out LobbyInfo info)
    {
        info = null;
        if (bytes == null || bytes.Length == 0)
            return false;
        return TryRead(new MessageReader().Init(bytes, 0, bytes.Length), out info);
    }

    /// <summary>The info as text for places that only hold strings (Steam lobby data).</summary>
    public string ToBase64() => Convert.ToBase64String(ToBytes());

    public static bool TryFromBase64(string text, out LobbyInfo info)
    {
        info = null;
        if (string.IsNullOrEmpty(text))
            return false;
        try
        {
            return TryFromBytes(Convert.FromBase64String(text), out info);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public override string ToString() => $"{DisplayName} ({Players}/{MaxPlayers}, game {GameVersion})";

    private static int Clamp(int value) => value < 0 ? 0 : value > 255 ? 255 : value;

    private static string Cut(string text, int max) => SafeText.Cut(text ?? "", max);

    // Names come from other people and end up in rich text and the console: no tags, no control characters.
    internal static string Clean(string text, int max)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var chars = new char[text.Length];
        int n = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '<' || c == '>' || char.IsControl(c) || SafeText.IsHidden(c) || SafeText.IsLoneSurrogate(text, i))
                continue;
            chars[n++] = c;
        }
        string clean = new string(chars, 0, n).Trim();
        return SafeText.Cut(clean, max);
    }
}
