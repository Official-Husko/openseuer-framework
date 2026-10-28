using System.Text;

namespace ObenCore.Tests;

/// <summary>Writes little-endian object bytes the way Unity serializes them (strings padded to four bytes).</summary>
internal sealed class ObjectBytes
{
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _writer;

    public ObjectBytes()
    {
        _writer = new BinaryWriter(_stream);
    }

    public ObjectBytes I32(int value) { _writer.Write(value); return this; }
    public ObjectBytes U32(uint value) { _writer.Write(value); return this; }
    public ObjectBytes I64(long value) { _writer.Write(value); return this; }
    public ObjectBytes U8(byte value) { _writer.Write(value); return this; }
    public ObjectBytes F32(float value) { _writer.Write(value); return this; }

    public ObjectBytes Floats(params float[] values)
    {
        foreach (float value in values)
            _writer.Write(value);
        return this;
    }

    public ObjectBytes Pptr(int fileId, long pathId) => I32(fileId).I64(pathId);

    public ObjectBytes Text(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        _writer.Write(bytes.Length);
        _writer.Write(bytes);
        return Align();
    }

    public ObjectBytes Raw(byte[] bytes) { _writer.Write(bytes); return this; }

    public ObjectBytes Align()
    {
        while (_stream.Length % 4 != 0)
            _writer.Write((byte)0);
        return this;
    }

    public byte[] ToArray() => _stream.ToArray();

    /// <summary>A GameObject: components, layer, name.</summary>
    public static byte[] GameObject(string name, params long[] components)
    {
        var o = new ObjectBytes().I32(components.Length);
        foreach (long component in components)
            o.Pptr(0, component);
        return o.I32(0).Text(name).ToArray();
    }

    /// <summary>A Transform at the origin with unit scale.</summary>
    public static byte[] Transform(long gameObject, long father, params long[] children)
    {
        var o = new ObjectBytes().Pptr(0, gameObject).Floats(0, 0, 0, 1).Floats(0, 0, 0).Floats(1, 1, 1).I32(children.Length);
        foreach (long child in children)
            o.Pptr(0, child);
        return o.Pptr(0, father).ToArray();
    }
}

/// <summary>
/// Builds a small serialized file in the layout <c>SceneLookFile</c> reads (format 22, little-endian
/// metadata, no type trees), for testing the reader without the game.
/// </summary>
internal sealed class SceneFileWriter
{
    private readonly List<(long PathId, int ClassId, byte[] Bytes)> _objects = new();
    private readonly List<string> _externals = new();

    public string UnityVersion = "6000.0.77f1";
    public int Format = 22;
    public byte TypeTrees;

    public SceneFileWriter Add(long pathId, int classId, byte[] bytes)
    {
        _objects.Add((pathId, classId, bytes));
        return this;
    }

    public SceneFileWriter External(string name)
    {
        _externals.Add(name);
        return this;
    }

    public void Write(string path) => File.WriteAllBytes(path, Build());

    public byte[] Build()
    {
        var classes = _objects.Select(o => o.ClassId).Distinct().ToList();
        var meta = new MemoryStream();
        var w = new BinaryWriter(meta);
        w.Write(Encoding.UTF8.GetBytes(UnityVersion));
        w.Write((byte)0);
        w.Write(19); // platform
        w.Write(TypeTrees);
        w.Write(classes.Count);
        foreach (int classId in classes)
        {
            w.Write(classId);
            w.Write((byte)0);
            w.Write((short)-1);
            if (classId == 114)
                w.Write(new byte[16]);
            w.Write(new byte[16]);
        }
        w.Write(_objects.Count);
        while (meta.Length % 4 != 0)
            w.Write((byte)0);
        long start = 0;
        foreach (var o in _objects)
        {
            w.Write(o.PathId);
            w.Write(start);
            w.Write((uint)o.Bytes.Length);
            w.Write(classes.IndexOf(o.ClassId));
            start += (o.Bytes.Length + 7) / 8 * 8;
        }
        w.Write(0); // scripts
        w.Write(_externals.Count);
        foreach (string external in _externals)
        {
            w.Write((byte)0);
            w.Write(new byte[16]);
            w.Write(0);
            w.Write(Encoding.UTF8.GetBytes(external));
            w.Write((byte)0);
        }
        w.Write(0); // ref types
        w.Write((byte)0); // user information
        byte[] metadata = meta.ToArray();

        long dataOffset = (48 + metadata.Length + 15) / 16 * 16;
        var file = new MemoryStream();
        file.Write(BigEndian(0));
        file.Write(BigEndian(0));
        file.Write(BigEndian(Format));
        file.Write(BigEndian(0));
        file.Write(new byte[] { 0, 0, 0, 0 });
        file.Write(BigEndian(metadata.Length));
        file.Write(BigEndian(0L));
        file.Write(BigEndian(dataOffset));
        file.Write(BigEndian(0L));
        file.Write(metadata);
        while (file.Length < dataOffset)
            file.WriteByte(0);
        foreach (var o in _objects)
        {
            file.Write(o.Bytes);
            while ((file.Length - dataOffset) % 8 != 0)
                file.WriteByte(0);
        }
        return file.ToArray();
    }

    private static byte[] BigEndian(int value) => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

    private static byte[] BigEndian(long value)
    {
        var bytes = new byte[8];
        for (int i = 0; i < 8; i++)
            bytes[i] = (byte)(value >> (56 - 8 * i));
        return bytes;
    }
}

/// <summary>A temporary folder that is deleted again.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "oben-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, true); }
        catch (IOException) { }
    }
}
