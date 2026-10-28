using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ObenCoop.Net.Messaging;

/// <summary>
/// Builds a binary message in a buffer that is reused. Little endian, no per-write allocations
/// (only the buffer growing the first time a bigger message is written). Rent one from
/// <see cref="MessagePool"/> and give it back when the message is sent. Not thread safe.
/// </summary>
public sealed class MessageWriter
{
    /// <summary>Longest string in bytes (UTF-8) that <see cref="WriteString"/> accepts.</summary>
    public const int MaxStringBytes = 4096;

    // Lenient: text from a player with a broken surrogate pair must not throw in the middle of a send.
    internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, false);

    private byte[] _buffer;

    public MessageWriter(int capacity = 256)
    {
        _buffer = new byte[Math.Max(16, capacity)];
    }

    /// <summary>The bytes written so far are <c>Buffer[0..Length]</c>. The array changes when it grows.</summary>
    public byte[] Buffer => _buffer;

    public int Length { get; private set; }

    public int Capacity => _buffer.Length;

    public void Reset() => Length = 0;

    public ArraySegment<byte> AsSegment() => new ArraySegment<byte>(_buffer, 0, Length);

    public void WriteByte(byte value)
    {
        Ensure(1);
        _buffer[Length++] = value;
    }

    public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    public void WriteUInt16(ushort value)
    {
        Ensure(2);
        _buffer[Length++] = (byte)value;
        _buffer[Length++] = (byte)(value >> 8);
    }

    public void WriteInt32(int value) => WriteUInt32((uint)value);

    public void WriteUInt32(uint value)
    {
        Ensure(4);
        for (int shift = 0; shift < 32; shift += 8)
            _buffer[Length++] = (byte)(value >> shift);
    }

    public void WriteInt64(long value)
    {
        Ensure(8);
        for (int shift = 0; shift < 64; shift += 8)
            _buffer[Length++] = (byte)(value >> shift);
    }

    public void WriteSingle(float value) => WriteInt32(new FloatBits { Float = value }.Int);

    public void WriteGuid(Guid value)
    {
        var bits = new GuidBits { Guid = value };
        WriteInt64(bits.Low);
        WriteInt64(bits.High);
    }

    /// <summary>Writes a length (1 to 2 bytes) and the UTF-8 text. Null is written as empty.</summary>
    public void WriteString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            WriteUInt16(0);
            return;
        }
        int bytes = Utf8.GetByteCount(value);
        if (bytes > MaxStringBytes)
            throw new ArgumentException($"A string in a message can be at most {MaxStringBytes} bytes, this one is {bytes}.", nameof(value));
        WriteUInt16((ushort)bytes);
        Ensure(bytes);
        Utf8.GetBytes(value, 0, value.Length, _buffer, Length);
        Length += bytes;
    }

    public void WriteBytes(byte[] source, int offset, int count)
    {
        Ensure(count);
        System.Buffer.BlockCopy(source, offset, _buffer, Length, count);
        Length += count;
    }

    public void WriteBytes(ArraySegment<byte> source) => WriteBytes(source.Array, source.Offset, source.Count);

    private void Ensure(int extra)
    {
        if (Length + extra <= _buffer.Length)
            return;
        int size = _buffer.Length * 2;
        while (size < Length + extra)
            size *= 2;
        Array.Resize(ref _buffer, size);
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
