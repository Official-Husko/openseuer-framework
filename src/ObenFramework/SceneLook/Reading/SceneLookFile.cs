// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;

namespace ObenFramework.SceneLook;

/// <summary>Where one object's bytes are in a file and what class it is.</summary>
internal struct SceneLookEntry
{
    public long Start;
    public int Size;
    public int ClassId;
}

/// <summary>Where a reference leads: the file and object, or why it leads nowhere.</summary>
internal sealed class SceneLookTarget
{
    public SceneLookFile File;
    public long PathId;

    /// <summary>"null" for an empty reference, otherwise a short reason; null when the target is there.</summary>
    public string Error;
}

/// <summary>
/// One of the game's serialized files (<c>levelN</c>, <c>sharedassetsN.assets</c>, <c>resources.assets</c>),
/// opened read-only. Only the build's own format is accepted (version 22, little-endian, no type trees, the
/// Unity version it was built with); anything else is refused when opening rather than misread later.
/// Objects are read on demand and parsed objects are kept in <see cref="Memo"/>.
/// </summary>
internal sealed class SceneLookFile : IDisposable
{
    /// <summary>The Unity version the object layouts were checked against.</summary>
    public const string UnityVersion = "6000.0.77f1";

    public const int FormatVersion = 22;
    public const int HeaderSize = 48;

    private readonly SceneLookFiles _files;
    private readonly FileStream _stream;
    private readonly long _dataOffset;
    private readonly Dictionary<long, SceneLookEntry> _entries = new Dictionary<long, SceneLookEntry>();
    private readonly List<long> _order = new List<long>();
    private readonly List<string> _externals = new List<string>();

    private SceneLookFile(SceneLookFiles files, string name, FileStream stream)
    {
        _files = files;
        Name = name;
        _stream = stream;
        byte[] header = ReadAt(stream, 0, HeaderSize);
        int version = BigEndianInt(header, 8);
        if (version != FormatVersion)
            throw new InvalidDataException("serialized format " + version + " is not handled");
        if (header[16] != 0)
            throw new InvalidDataException("big-endian metadata is not handled");
        int metadataSize = BigEndianInt(header, 20);
        long start = BigEndianLong(header, 32);
        long length = stream.Length;
        if (metadataSize <= 0 || HeaderSize + (long)metadataSize > length || start < 0 || start > length)
            throw new InvalidDataException("metadata is outside the file");
        _dataOffset = start;

        var cursor = new SceneLookCursor(ReadAt(stream, HeaderSize, metadataSize), 0);
        UnityString = cursor.CString();
        if (UnityString != UnityVersion)
            throw new InvalidDataException("Unity version " + UnityString + " is not " + UnityVersion);
        cursor.I32(); // target platform
        if (cursor.U8() != 0)
            throw new InvalidDataException("type trees are not handled");

        int types = cursor.Count();
        if (types > cursor.Length)
            throw new InvalidDataException("type count is larger than the metadata");
        var classIds = new int[types];
        for (int i = 0; i < types; i++)
        {
            classIds[i] = cursor.I32();
            cursor.U8();  // stripped
            cursor.I16(); // script type index
            if (classIds[i] == SceneLookClass.MonoBehaviour)
                cursor.Skip(16); // script id
            cursor.Skip(16); // type hash
        }

        int objects = cursor.Count();
        cursor.Align(4);
        long room = length - _dataOffset;
        for (int i = 0; i < objects; i++)
        {
            long pathId = cursor.I64();
            long objectStart = cursor.I64();
            uint size = cursor.U32();
            int typeIndex = cursor.I32();
            if (size > (uint)int.MaxValue || typeIndex < 0 || typeIndex >= types || objectStart < 0 || objectStart > room || (long)size > room - objectStart)
                throw new InvalidDataException("object record " + pathId + " is invalid");
            _entries[pathId] = new SceneLookEntry { Start = objectStart, Size = (int)size, ClassId = classIds[typeIndex] };
            _order.Add(pathId);
        }

        int scripts = cursor.Count();
        cursor.Skip(12L * scripts);
        int external = cursor.Count();
        for (int i = 0; i < external; i++)
        {
            cursor.CString(); // empty
            cursor.Skip(16);  // guid
            cursor.I32();     // type
            _externals.Add(cursor.CString());
        }
        int refTypes = cursor.Count();
        for (int i = 0; i < refTypes; i++)
        {
            cursor.I32();
            cursor.U8();
            cursor.I16();
            cursor.Skip(16);
            cursor.Skip(16);
        }
        cursor.CString(); // user information
        if (cursor.Position != cursor.Length)
            throw new InvalidDataException("metadata is not fully read (" + cursor.Position + " of " + cursor.Length + ")");
    }

    public string Name { get; }

    public string UnityString { get; }

    /// <summary>Path ids in the order the file lists them.</summary>
    public IList<long> Order => _order;

    /// <summary>The names of the files this one refers to, as written (file id n is entry n-1).</summary>
    public IList<string> Externals => _externals;

    public SceneLookMemo Memo { get; } = new SceneLookMemo();

    /// <summary>How many object bytes have been read, for diagnostics.</summary>
    public long BytesRead { get; private set; }

    public static SceneLookFile Open(SceneLookFiles files, string path, string name, out string reason)
    {
        reason = null;
        FileStream stream = null;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new SceneLookFile(files, name, stream);
        }
        catch (Exception e)
        {
            stream?.Dispose();
            reason = e.Message;
            return null;
        }
    }

    public bool Contains(long pathId) => _entries.ContainsKey(pathId);

    public int ClassId(long pathId) => Entry(pathId).ClassId;

    public byte[] ReadObject(long pathId) => ReadObjectHead(pathId, int.MaxValue);

    /// <summary>The first <paramref name="limit"/> bytes of an object (all of it when shorter).</summary>
    public byte[] ReadObjectHead(long pathId, int limit)
    {
        var entry = Entry(pathId);
        int count = Math.Min(limit, entry.Size);
        byte[] bytes = ReadAt(_stream, _dataOffset + entry.Start, count);
        BytesRead += count;
        return bytes;
    }

    /// <summary>Follows a reference, opening the external file it points into when needed.</summary>
    public SceneLookTarget Target(SceneLookRef reference)
    {
        var target = new SceneLookTarget { PathId = reference.PathId };
        if (reference.PathId == 0)
        {
            target.Error = "null";
            return target;
        }
        if (reference.FileId == 0)
        {
            target.File = this;
        }
        else if (reference.FileId < 0 || reference.FileId > _externals.Count)
        {
            target.Error = "bad fileID";
            return target;
        }
        else
        {
            target.File = _files.Get(_externals[reference.FileId - 1], out _);
            if (target.File == null)
            {
                target.Error = "external not installed";
                return target;
            }
        }
        if (!target.File.Contains(reference.PathId))
            target.Error = "pptr target missing";
        return target;
    }

    public static byte[] ReadAt(FileStream stream, long offset, int count)
    {
        var bytes = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);
        int done = 0;
        while (done < count)
        {
            int read = stream.Read(bytes, done, count - done);
            if (read <= 0)
                throw new InvalidDataException("file ends early");
            done += read;
        }
        return bytes;
    }

    public void Dispose() => _stream.Dispose();

    private SceneLookEntry Entry(long pathId)
    {
        if (!_entries.TryGetValue(pathId, out var entry))
            throw new InvalidDataException("object " + pathId + " is not in " + Name);
        return entry;
    }

    private static int BigEndianInt(byte[] bytes, int at) =>
        (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];

    private static long BigEndianLong(byte[] bytes, int at)
    {
        long value = 0;
        for (int i = 0; i < 8; i++)
            value = (value << 8) | bytes[at + i];
        return value;
    }
}
