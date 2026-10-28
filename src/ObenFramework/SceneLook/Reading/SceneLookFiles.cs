// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;

namespace ObenFramework.SceneLook;

/// <summary>
/// The open serialized files of the game's data folder, by file name. Only plain names directly in the
/// folder are opened (references to Unity's built-in resources are refused, which only matters for the
/// rare look that uses them). A file that fails to open is remembered with its reason. Dispose closes them.
/// </summary>
internal sealed class SceneLookFiles : IDisposable
{
    private readonly string _dataDir;
    private readonly Dictionary<string, SceneLookFile> _open = new Dictionary<string, SceneLookFile>();
    private readonly Dictionary<string, string> _refused = new Dictionary<string, string>();

    public SceneLookFiles(string dataDir)
    {
        _dataDir = dataDir;
    }

    public string DataDir => _dataDir;

    public int OpenCount => _open.Count;

    public SceneLookFile Get(string name, out string reason)
    {
        reason = null;
        if (name != null && _open.TryGetValue(name, out var file))
            return file;
        if (name != null && _refused.TryGetValue(name, out reason))
            return null;

        if (!IsPlainName(name))
        {
            reason = "bad file name " + name;
        }
        else if (!Installed(name, 0))
        {
            reason = "not installed " + name;
        }
        else
        {
            file = SceneLookFile.Open(this, Path.Combine(_dataDir, name), name, out reason);
            if (file != null)
            {
                _open[name] = file;
                return file;
            }
        }
        if (name != null)
            _refused[name] = reason;
        return null;
    }

    /// <summary>Closes every file but <paramref name="keep"/>; a scan opens many and only needs them while it runs.</summary>
    public void CloseAllExcept(string keep)
    {
        foreach (string name in new List<string>(_open.Keys))
        {
            if (name == keep)
                continue;
            _open[name].Dispose();
            _open.Remove(name);
        }
    }

    /// <summary>Bytes of a resource stream file (<c>.resS</c>, <c>.resource</c>), bounds-checked.</summary>
    public byte[] ReadStream(string name, long offset, long count)
    {
        if (!IsPlainName(name))
            throw new InvalidDataException("bad stream name " + name);
        if (count < 0 || count > int.MaxValue)
            throw new InvalidDataException("bad stream size " + count);
        using (var stream = new FileStream(Path.Combine(_dataDir, name), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (offset < 0 || offset > stream.Length - count)
                throw new InvalidDataException("stream is outside " + name);
            return SceneLookFile.ReadAt(stream, offset, (int)count);
        }
    }

    /// <summary>The file exists in the data folder and is at least <paramref name="minimumLength"/> bytes long.</summary>
    public bool Installed(string name, long minimumLength)
    {
        if (!IsPlainName(name))
            return false;
        try
        {
            string path = Path.Combine(_dataDir, name);
            return File.Exists(path) && new FileInfo(path).Length >= minimumLength;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsPlainName(string name) =>
        !string.IsNullOrEmpty(name) && Path.GetFileName(name) == name && name.IndexOf(':') < 0;

    public void Dispose()
    {
        foreach (var file in _open.Values)
            file.Dispose();
        _open.Clear();
        _refused.Clear();
    }
}
