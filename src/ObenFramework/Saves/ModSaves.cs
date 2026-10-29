using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenFramework.Saves;

/// <summary>
/// Per-save data for mods. A mod registers a plain class once and gets a <see cref="ModSaveData{T}"/>;
/// the framework stores it as JSON in <c>&lt;save folder&gt;/mods/&lt;modId&gt;[.key].json</c> after every game
/// save and loads it again when a save loads. The game's own save files are never touched.
/// See docs/modding/framework/saves.md for the reasoning behind the timing.
/// </summary>
public static class ModSaves
{
    internal static ModLogger Log;

    private static readonly Dictionary<string, ISaveSlot> Slots = new Dictionary<string, ISaveSlot>();

    // Counts loads and main menu visits. Data parked for a reloading mod is only trusted if no load
    // happened in between; otherwise the file of the newly loaded save is the truth.
    private static int _epoch;
    private const string EpochKey = "ObenFramework.ModSaves.Epoch";

    /// <summary>
    /// Registers a data class for <paramref name="owner"/>. Pass <paramref name="key"/> to keep more than
    /// one object per mod. <typeparamref name="T"/> needs a parameterless constructor whose defaults are what
    /// a new game starts with, and must be something Newtonsoft.Json can round trip (public fields and
    /// properties, lists, dictionaries, enums). The registration ends when the owner unloads; the data is
    /// handed to the next copy of the mod when it reloads.
    /// </summary>
    public static ModSaveData<T> Register<T>(ObenMod owner, string key = null) where T : class, new()
    {
        var data = new ModSaveData<T>(owner.Info.Id, key);
        if (Slots.ContainsKey(data.FileName))
            throw new ArgumentException($"{owner.Info.Id} already registered save data{(key == null ? "" : $" '{key}'")}. Pass a different key.");

        Slots[data.FileName] = data;
        owner.Track(Disposable.Create(() => Unregister(data)));

        // Handed over by the previous copy of this mod, else read from the loaded save when the
        // mod arrives after the save loaded.
        if (!TryTakeHandoff(data))
        {
            string folder = WorldState.InWorld ? SaveFolders.LoadedFolder() : null;
            if (folder != null)
                Guard(data, () => ((ISaveSlot)data).ReadFrom(folder));
        }
        return data;
    }

    /// <summary>
    /// Reads what a mod (yours or another) stored in the loaded save, without registering: a copy, so
    /// changing it changes nothing. Null at the main menu, in a new game, when there is no such file or it
    /// can't be read as <typeparamref name="T"/>. Missing nested lists and objects are filled in, as for
    /// registered data.
    /// </summary>
    public static T Peek<T>(string modId, string key = null) where T : class
    {
        if (string.IsNullOrEmpty(modId))
            throw new ArgumentException("Name the mod whose data to read.", nameof(modId));
        string folder = WorldState.InWorld ? SaveFolders.LoadedFolder() : null;
        if (folder == null)
            return null;
        string path = SaveFolders.FileFor(folder, SaveFolders.FileName(modId, key));
        if (!System.IO.File.Exists(path) || new System.IO.FileInfo(path).Length > SaveFolders.MaxModFileBytes)
            return null;
        if (!ObenLoader.Config.JsonFile.TryRead<SaveEnvelope<T>>(path, out var envelope, out _) || envelope.Data == null)
            return null;
        try { SaveBlanks.Fill(envelope.Data); }
        catch (Exception e) { Log.Warn($"Couldn't fill in the missing parts of {path}: {e.Message}"); }
        return envelope.Data;
    }

    internal static void Init(ObenMod framework)
    {
        Log = framework.Log;
        if (ReloadState.Take(EpochKey, out int epoch))
            _epoch = epoch;

        GameEvents.Saved.Subscribe(framework, OnSaved);
        GameEvents.Loading.Subscribe(framework, OnLoading);
        GameEvents.MainMenu.Subscribe(framework, OnMainMenu);
        framework.Track(Disposable.Create(Slots.Clear));
    }

    private static void OnSaved(string folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            Log.Warn("The save folder is unknown; mod data was not written.");
            return;
        }
        foreach (var slot in Snapshot())
            slot.WriteTo(folder);
    }

    private static void OnLoading()
    {
        NextEpoch();
        string folder = SaveFolders.LoadedFolder();
        foreach (var slot in Snapshot())
            Guard(slot, () => slot.ReadFrom(folder));
    }

    private static void OnMainMenu()
    {
        NextEpoch();
        foreach (var slot in Snapshot())
            Guard(slot, slot.Reset);
    }

    private static void NextEpoch()
    {
        _epoch++;
        ReloadState.Put(EpochKey, _epoch);
    }

    private static ISaveSlot[] Snapshot()
    {
        var all = new ISaveSlot[Slots.Count];
        Slots.Values.CopyTo(all, 0);
        return all;
    }

    private static string HandoffKey(ISaveSlot slot) => "ObenFramework.ModSaves." + slot.FileName;

    private static void Unregister(ISaveSlot slot)
    {
        if (!Slots.Remove(slot.FileName))
            return;
        try { ReloadState.Put(HandoffKey(slot), _epoch + "\n" + slot.Snapshot()); }
        catch (Exception e) { Log.Error($"Couldn't keep {slot.FileName} across the unload", e); }
    }

    private static bool TryTakeHandoff(ISaveSlot slot)
    {
        if (!ReloadState.Take(HandoffKey(slot), out string parked))
            return false;
        int split = parked.IndexOf('\n');
        if (split < 0 || !int.TryParse(parked.Substring(0, split), out int epoch) || epoch != _epoch)
            return false;
        Guard(slot, () => slot.Restore(parked.Substring(split + 1)));
        return true;
    }

    private static void Guard(ISaveSlot slot, Action action)
    {
        try { action(); }
        catch (Exception e) { Log.Error($"Loading {slot.FileName} failed", e); }
    }
}
