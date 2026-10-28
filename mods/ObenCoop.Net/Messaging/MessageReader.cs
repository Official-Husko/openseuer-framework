using System;
using System.Runtime.InteropServices;

namespace ObenCoop.Net.Messaging;

/// <summary>Raised when a message is shorter than what the reader asked for, or otherwise malformed.</summary>
public sealed class MessageFormatException : Exception
{
    public MessageFormatException(string message) : base(message) { }
}

/// <summary>
/// Reads the values <see cref="MessageWriter"/> wrote, straight from the received buffer (nothing is
/// copied except strings, which have to become strings). Every read is bounds checked and throws
/// <see cref="MessageFormatException"/> on a short or damaged message, so handlers never read past
/// the end. Rent from <see cref="MessagePool"/>; the buffer is only valid during the handler call.
/// </summary>
public sealed class MessageReader
{
    private byte[] _buffer;
    private int _position;
    private int _end;

    /// <summary>Points the reader at a buffer region and rewinds it.</summary>
    public MessageReader Init(byte[] buffer, int offset, int count)
    {
        _buffer = buffer;
        _position = offset;
        _end = offset + count;
        return this;
    }

    public MessageReader Init(ArraySegment<byte> data) => Init(data.Array, data.Offset, data.Count);

    public int Remaining => _end - _position;

    public byte ReadByte()
    {
        Need(1);
        return _buffer[_position++];
    }

    public bool ReadBool() => ReadByte() != 0;

    public ushort ReadUInt16()
    {
        Need(2);
        ushort value = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
        _position += 2;
        return value;
    }

    public int ReadInt32() => (int)ReadUInt32();

    public uint ReadUInt32()
    {
        Need(4);
        uint value = 0;
        for (int i = 0; i < 4; i++)
            value |= (uint)_buffer[_position + i] << (8 * i);
        _position += 4;
        return value;
    }

    public long ReadInt64()
    {
        Need(8);
        long value = 0;
        for (int i = 0; i < 8; i++)
            value |= (long)_buffer[_position + i] << (8 * i);
        _position += 8;
        return value;
    }

    public float ReadSingle() => new FloatBits { Int = ReadInt32() }.Float;

    public Guid ReadGuid()
    {
        long low = ReadInt64();
        long high = ReadInt64();
        return new GuidBits { Low = low, High = high }.Guid;
    }

    public string ReadString()
    {
        int bytes = ReadUInt16();
        if (bytes == 0)
            return string.Empty;
        if (bytes > MessageWriter.MaxStringBytes)
            throw new MessageFormatException($"String of {bytes} bytes is over the limit.");
        Need(bytes);
        string value = MessageWriter.Utf8.GetString(_buffer, _position, bytes);
        _position += bytes;
        return value;
    }

    /// <summary>
    /// The UTF-8 bytes of a string <see cref="MessageWriter.WriteString"/> wrote, as a view into the
    /// buffer, without making a string. For hot paths that only need a string when it changed.
    /// </summary>
    public ArraySegment<byte> ReadStringBytes()
    {
        int bytes = ReadUInt16();
        if (bytes > MessageWriter.MaxStringBytes)
            throw new MessageFormatException($"String of {bytes} bytes is over the limit.");
        Need(bytes);
        var segment = new ArraySegment<byte>(_buffer, _position, bytes);
        _position += bytes;
        return segment;
    }

    /// <summary>Copies <paramref name="count"/> bytes into <paramref name="destination"/>.</summary>
    public void ReadBytes(byte[] destination, int offset, int count)
    {
        Need(count);
        Buffer.BlockCopy(_buffer, _position, destination, offset, count);
        _position += count;
    }

    /// <summary>The rest of the message as a view into the buffer, and moves to the end.</summary>
    public ArraySegment<byte> ReadRest()
    {
        var rest = new ArraySegment<byte>(_buffer, _position, Remaining);
        _position = _end;
        return rest;
    }

    public void Skip(int count)
    {
        Need(count);
        _position += count;
    }

    internal void Clear()
    {
        _buffer = null;
        _position = _end = 0;
    }

    private void Need(int count)
    {
        if (count < 0 || _position + count > _end)
            throw new MessageFormatException($"Message ended early: needed {count} more bytes, {Remaining} left.");
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct FloatBits
    {
        [FieldOffset(0)] public float Float;
        [FieldOffset(0)] public int Int;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct GuidBits
    {
        [FieldOffset(0)] public Guid Guid;
        [FieldOffset(0)] public long Low;
        [FieldOffset(8)] public long High;
    }
}
