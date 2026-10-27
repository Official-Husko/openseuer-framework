using System;
using ObenLoader.Logging;

namespace ObenLoader.Config;

/// <summary>
/// A typed settings object backed by a JSON file. Missing files and missing fields fall back
/// to the defaults set in <typeparamref name="T"/>'s constructor or field initialisers.
/// </summary>
public sealed class ConfigFile<T> where T : class, new()
{
    private readonly ModLogger _log;

    public string Path { get; }
    public T Value { get; private set; }

    /// <summary>Raised after <see cref="Save"/> or <see cref="Reload"/>.</summary>
    public event Action<T> Changed;

    public ConfigFile(string path, ModLogger log)
    {
        Path = path;
        _log = log;
        Reload();
    }

    public void Reload()
    {
        try
        {
            Value = JsonFile.Read<T>(Path);
        }
        catch (Exception e)
        {
            _log?.Warn($"Couldn't read {Path}, using defaults. {e.Message}");
            Value = new T();
        }
        Changed?.Invoke(Value);
    }

    public void Save()
    {
        try
        {
            JsonFile.Write(Path, Value);
        }
        catch (Exception e)
        {
            _log?.Error($"Couldn't write {Path}", e);
        }
        Changed?.Invoke(Value);
    }
}
