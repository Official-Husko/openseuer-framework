// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.IO;
using System.Text;

namespace ObenFramework.SceneLook;

/// <summary>
/// Reads little-endian values from one serialized object's bytes. Every read is bounds-checked and throws
/// <see cref="InvalidDataException"/> past the end, so a layout that doesn't match fails cleanly.
/// </summary>
internal sealed class SceneLookCursor
{
    private readonly byte[] _data;
    private int _position;

    public SceneLookCursor(byte[] data, int position)
    {
        _data = data;
        _position = position;
    }

    public int Position => _position;

    public int Length => _data.Length;

    public byte U8()
    {
        Need(1);
        return _data[_position++];
    }

    public ushort U16()
    {
        Need(2);
        ushort value = BitConverter.ToUInt16(_data, _position);
        _position += 2;
        return value;
    }

    public short I16()
    {
        Need(2);
        short value = BitConverter.ToInt16(_data, _position);
        _position += 2;
        return value;
    }

    public int I32()
    {
        Need(4);
        int value = BitConverter.ToInt32(_data, _position);
        _position += 4;
        return value;
    }

    public uint U32()
    {
        Need(4);
        uint value = BitConverter.ToUInt32(_data, _position);
        _position += 4;
        return value;
    }

    public long I64()
    {
        Need(8);
        long value = BitConverter.ToInt64(_data, _position);
        _position += 8;
        return value;
    }

    public float F32()
    {
        Need(4);
        float value = BitConverter.ToSingle(_data, _position);
        _position += 4;
        return value;
    }

    /// <summary>An array length: never negative.</summary>
    public int Count()
    {
        int count = I32();
        if (count < 0)
            throw new InvalidDataException("negative count " + count);
        return count;
    }

    /// <summary>A reference to another object: file index (0 is this file) and path id.</summary>
    public SceneLookRef Pptr() => new SceneLookRef(I32(), I64());

    public float[] Floats(int count)
    {
        Need(4L * count);
        var values = new float[count];
        for (int i = 0; i < count; i++)
            values[i] = F32();
        return values;
    }

    public byte[] Raw(long count)
    {
        Need(count);
        int size = (int)count;
        var copy = new byte[size];
        Buffer.BlockCopy(_data, _position, copy, 0, size);
        _position += size;
        return copy;
    }

    public void Skip(long count)
    {
        Need(count);
        _position += (int)count;
    }

    /// <summary>Moves to the next multiple of <paramref name="n"/> (a power of two), as Unity pads after bytes and strings.</summary>
    public void Align(int n) => _position = (_position + n - 1) & ~(n - 1);

    /// <summary>A length-prefixed UTF-8 string, padded to four bytes.</summary>
    public string Text()
    {
        int count = I32();
        Need(count);
        string text = Encoding.UTF8.GetString(_data, _position, count);
        _position += count;
        Align(4);
        return text;
    }

    /// <summary>A zero-terminated UTF-8 string (the file header uses these).</summary>
    public string CString()
    {
        int end = Array.IndexOf(_data, (byte)0, _position);
        if (end < 0)
            throw new InvalidDataException("unterminated string");
        string text = Encoding.UTF8.GetString(_data, _position, end - _position);
        _position = end + 1;
        return text;
    }

    private void Need(long count)
    {
        if (count < 0 || count > _data.Length - _position)
            throw new InvalidDataException("read of " + count + " bytes is past the end of the object");
    }
}
