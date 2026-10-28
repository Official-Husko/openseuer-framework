using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// Short codes for joining a game, read out or pasted in place of an address: <c>7K2Q-9MXA-B4C1</c>.
/// A code holds what it points at (an IPv4 address and port, or a Steam lobby id), a 4-bit kind and an
/// 8-bit CRC, written in Crockford's base32 (no I, L, O or U, case doesn't matter, dashes and spaces are
/// ignored, and O, I and L are read as 0 and 1). The CRC catches every single wrong character, so a
/// typo is reported instead of connecting somewhere else.
/// Direct codes are 12 characters (4 + 32 + 16 + 8 bits), Steam codes 16 (4 + 64 + 8 bits, padded).
/// </summary>
public static class JoinCode
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int DirectChars = 12;
    private const int SteamChars = 16;

    /// <summary>The code for a game at an IPv4 address and port.</summary>
    /// <exception cref="ArgumentException">Not an IPv4 address, or the port is out of range.</exception>
    public static string ForAddress(string ipv4, int port)
    {
        if (!IPAddress.TryParse(ipv4 ?? "", out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException($"'{ipv4}' is not an IPv4 address.", nameof(ipv4));
        if (port < 1 || port > 65535)
            throw new ArgumentException("The port must be 1 to 65535.", nameof(port));

        var data = new byte[7];
        data[0] = (byte)LobbyKind.Direct;
        Buffer.BlockCopy(ip.GetAddressBytes(), 0, data, 1, 4);
        data[5] = (byte)(port >> 8);
        data[6] = (byte)port;
        return Encode(data, DirectChars);
    }

    /// <summary>The code for a Steam lobby.</summary>
    public static string ForSteamLobby(ulong lobbyId)
    {
        var data = new byte[9];
        data[0] = (byte)LobbyKind.Steam;
        for (int i = 0; i < 8; i++)
            data[1 + i] = (byte)(lobbyId >> (56 - 8 * i));
        return Encode(data, SteamChars);
    }

    /// <summary>True when the text has the shape of a code (right length, only code characters), typo or not.</summary>
    public static bool LooksLikeCode(string text)
    {
        string clean = Normalise(text);
        return clean != null && (clean.Length == DirectChars || clean.Length == SteamChars);
    }

    /// <summary>Reads a code. False when it isn't one or its check doesn't match.</summary>
    public static bool TryDecode(string text, out LobbyAddress address)
    {
        address = default;
        string clean = Normalise(text);
        if (clean == null || (clean.Length != DirectChars && clean.Length != SteamChars))
            return false;

        var bits = new BitReader(clean);
        int kind = bits.Read(4);
        if (kind == (int)LobbyKind.Direct && clean.Length == DirectChars)
        {
            var data = new byte[7];
            data[0] = (byte)kind;
            for (int i = 1; i < 7; i++)
                data[i] = (byte)bits.Read(8);
            if (bits.Read(8) != Crc8(data))
                return false;
            string ip = $"{data[1]}.{data[2]}.{data[3]}.{data[4]}";
            int port = (data[5] << 8) | data[6];
            if (port == 0)
                return false;
            address = LobbyAddress.Direct(ip, port);
            return true;
        }
        if (kind == (int)LobbyKind.Steam && clean.Length == SteamChars)
        {
            var data = new byte[9];
            data[0] = (byte)kind;
            ulong id = 0;
            for (int i = 1; i < 9; i++)
            {
                data[i] = (byte)bits.Read(8);
                id = (id << 8) | data[i];
            }
            if (bits.Read(8) != Crc8(data) || bits.Read(4) != 0 || id == 0)
                return false;
            address = LobbyAddress.Steam(id);
            return true;
        }
        return false;
    }

    internal static byte Crc8(byte[] data)
    {
        int crc = 0;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x80) != 0 ? ((crc << 1) ^ 0x07) & 0xFF : (crc << 1) & 0xFF;
        }
        return (byte)crc;
    }

    // Kind as 4 bits, the rest of the data as bytes, the CRC, zero padding, in groups of four.
    private static string Encode(byte[] data, int chars)
    {
        var bits = new BitWriter(chars * 5);
        bits.Write(data[0], 4);
        for (int i = 1; i < data.Length; i++)
            bits.Write(data[i], 8);
        bits.Write(Crc8(data), 8);

        var text = new StringBuilder(chars + chars / 4);
        for (int i = 0; i < chars; i++)
        {
            if (i > 0 && i % 4 == 0)
                text.Append('-');
            text.Append(Alphabet[bits.Group(i)]);
        }
        return text.ToString();
    }

    // Upper case, separators dropped, look-alike letters mapped. Null when a character can't be in a code.
    private static string Normalise(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var clean = new StringBuilder(text.Length);
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (c == '-' || c == ' ')
                continue;
            if (c == 'O')
                c = '0';
            else if (c == 'I' || c == 'L')
                c = '1';
            if (Alphabet.IndexOf(c) < 0)
                return null;
            clean.Append(c);
        }
        return clean.ToString();
    }

    private sealed class BitWriter
    {
        private readonly bool[] _bits;
        private int _length;

        public BitWriter(int size) => _bits = new bool[size];

        public void Write(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
                _bits[_length++] = ((value >> i) & 1) != 0;
        }

        public int Group(int index)
        {
            int value = 0;
            for (int i = 0; i < 5; i++)
                value = (value << 1) | (_bits[index * 5 + i] ? 1 : 0);
            return value;
        }
    }

    private sealed class BitReader
    {
        private readonly string _text;
        private int _position;

        public BitReader(string text) => _text = text;

        public int Read(int count)
        {
            int value = 0;
            for (int i = 0; i < count; i++, _position++)
            {
                int symbol = Alphabet.IndexOf(_text[_position / 5]);
                int bit = (symbol >> (4 - _position % 5)) & 1;
                value = (value << 1) | bit;
            }
            return value;
        }
    }
}
