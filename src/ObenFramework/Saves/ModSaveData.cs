using System;
using System.IO;
using Newtonsoft.Json;
using ObenFramework.Events;
using ObenLoader.Config;

namespace ObenFramework.Saves;

/// <summary>
/// A mod's own data for the current save, kept in a side file next to the game's files. Get one from
/// <see cref="ModSaves.Register{T}"/>. <see cref="Value"/> is the live object; the framework writes it
/// after each game save and replaces it whenever a save loads, so read it through the property each
/// time and don't keep the reference around.
/// </summary>
public sealed class ModSaveData<T> : ISaveSlot where T : class, new()
{
    private T _value = new T();

    internal ModSaveData(string modId, string key)
    {
        ModId = modId;
        Key = key;
        FileName = SaveFolders.FileName(modId, key);
    }

    /// <summary>Id of the mod that owns the data.</summary>
    public string ModId { get; }

    /// <summary>Distinguishes several objects of one mod, or null for the mod's only one.</summary>
    public string Key { get; }

    /// <summary>The data. Never null. Defaults to <c>new T()</c> in a new game and at the main menu.</summary>
    public T Value => _value;

    /// <summary>
    /// Raised after <see cref="Value"/> was replaced by a save's content or reset to defaults, which happens
    /// when a save starts loading, at the main menu and (for a mod loaded mid-game) on registration. The world
    /// is not restored yet at that point; apply data to it in <see cref="GameEvents.WorldReady"/>.
    /// </summary>
    public GameEvent<T> DataLoaded { get; } = new GameEvent<T>("ModSaveData.DataLoaded");

    internal string FileName { get; }

    string ISaveSlot.FileName => FileName;

    /// <summary>Throws the current data away and starts over with defaults.</summary>
    public void Reset()
    {
        _value = new T();
        Announce();
    }

    void ISaveSlot.Reset() => Reset();

    void ISaveSlot.ReadFrom(string saveFolder)
    {
        string path = saveFolder == null ? null : SaveFolders.FileFor(saveFolder, FileName);
        if (path == null || !File.Exists(path))
        {
            Reset();
            return;
        }

        if (new FileInfo(path).Length > SaveFolders.MaxModFileBytes)
        {
            ModSaves.Log.Error($"{path} is over {SaveFolders.MaxModFileBytes / (1024 * 1024)} MB; using defaults for {ModId}. The file is kept as .corrupt.");
            KeepCorrupt(path);
            Reset();
            return;
        }

        if (JsonFile.TryRead<SaveEnvelope<T>>(path, out var envelope, out var error) && envelope.Data != null)
        {
            if (envelope.Format > SaveEnvelope<T>.CurrentFormat)
                ModSaves.Log.Warn($"{FileName} has file format {envelope.Format}, newer than this framework knows; reading it anyway.");
            _value = envelope.Data;
            Announce();
            return;
        }

        ModSaves.Log.Error($"Couldn't read {path}; using defaults for {ModId}. The file is kept as .corrupt.", error);
        KeepCorrupt(path);
        Reset();
    }

    void ISaveSlot.WriteTo(string saveFolder)
    {
        try
        {
            JsonFile.Write(SaveFolders.FileFor(saveFolder, FileName), new SaveEnvelope<T> { Data = _value });
        }
        catch (Exception e)
        {
            ModSaves.Log.Error($"Couldn't write {FileName} into {saveFolder}", e);
        }
    }

    string ISaveSlot.Snapshot() => JsonConvert.SerializeObject(_value, JsonFile.Settings);

    void ISaveSlot.Restore(string json)
    {
        _value = JsonConvert.DeserializeObject<T>(json, JsonFile.Settings) ?? new T();
        Announce();
    }

    private void Announce() => DataLoaded.Raise(_value, ModSaves.Log);

    // A broken file would be overwritten by the next save; keep it so the data isn't lost for good.
    private static void KeepCorrupt(string path)
    {
        try { File.Copy(path, path + ".corrupt", true); }
        catch (IOException) { }
    }
}
