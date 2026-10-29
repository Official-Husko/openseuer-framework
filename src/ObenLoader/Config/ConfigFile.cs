using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenLoader.Config;

/// <summary>
/// A typed settings object backed by a JSON file. Missing files and missing fields fall back
/// to the defaults set in <typeparamref name="T"/>'s constructor or field initialisers. Every value that
/// changes on a save or reload is written to the mod's log ("StackSize 2 -> 3").
/// </summary>
public sealed class ConfigFile<T> : IConfigFile where T : class, new()
{
    private readonly ModLogger _log;

    // The values as last read or written, for the change log.
    private JObject _saved;

    public string Path { get; }
    public T Value { get; private set; }

    /// <summary>A fresh object with the default values (what a missing file gives).</summary>
    public T Defaults => new T();

    Type IConfigFile.ValueType => typeof(T);
    object IConfigFile.Value => Value;
    object IConfigFile.Defaults => Defaults;

    /// <summary>Raised after <see cref="Save"/> or <see cref="Reload"/>. A handler that throws is logged, not passed on.</summary>
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
        LogChanges();
        Raise();
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
        LogChanges();
        Raise();
    }

    /// <summary>Puts every value back to its default (in the same <see cref="Value"/> object) and saves.</summary>
    public void ResetToDefaults()
    {
        ConfigValues.CopyInto(new T(), Value);
        Save();
    }

    /// <summary>
    /// Calls <paramref name="changed"/> with the new value whenever <paramref name="read"/> gives something else
    /// after a save or reload, until <paramref name="owner"/> unloads: a change event for one setting.
    /// </summary>
    public void Watch<TValue>(ITracker owner, Func<T, TValue> read, Action<TValue> changed)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        TValue last = read(Value);
        void Check(T value)
        {
            TValue now = read(value);
            if (EqualityComparer<TValue>.Default.Equals(now, last))
                return;
            last = now;
            changed(now);
        }
        Changed += Check;
        owner.Track(Disposable.Create(() => Changed -= Check));
    }

    private void LogChanges()
    {
        try
        {
            var now = ConfigValues.Snapshot(Value);
            string changes = _saved == null ? null : ConfigValues.Changes(_saved, now);
            _saved = now;
            if (changes != null)
                _log?.Info($"{System.IO.Path.GetFileName(Path)}: {changes}");
        }
        catch (Exception e)
        {
            _log?.Warn($"Couldn't compare {Path} with its last values: {e.Message}");
        }
    }

    private void Raise()
    {
        var handlers = Changed;
        if (handlers == null)
            return;
        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(Value);
            }
            catch (Exception e)
            {
                _log?.Error($"A change handler of {System.IO.Path.GetFileName(Path)} failed", e);
            }
        }
    }
}
