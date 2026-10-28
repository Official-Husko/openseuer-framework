// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine.SceneManagement;

namespace ObenFramework.Items;

/// <summary>
/// Containers in scenes that are not loaded: reading what the save says is in them, and queueing items to put
/// in or take out. A queued change is kept with the save and applied the next time the player enters that
/// scene, through the game's own storage methods; <see cref="Applied"/> or <see cref="Failed"/> reports the
/// result. Use <see cref="World.Containers"/> for containers in the loaded scene.
/// </summary>
/// <remarks>
/// Containers are named by their GUID, as <see cref="World.Containers.IdOf"/> gives it while their scene is
/// loaded (and <see cref="ContainerIds"/> lists them). Containers inside furniture the player placed get a new
/// GUID with every save, so only the scene's own containers can be used. Reading needs a loaded save and works
/// from the save folder the game loaded last, which holds the latest state of every visited scene; a scene the
/// player has never visited has no file yet. The queue is part of the save: a change queued and never saved
/// is gone after a reload, like any other unsaved progress. Take changes are all or nothing.
/// </remarks>
public static class UnloadedStorage
{
    internal static readonly HashSet<UnloadedStorageEntry> Settled = new HashSet<UnloadedStorageEntry>();

    private static ModSaveData<UnloadedStorageFile> _data;

    /// <summary>A queued change went into its container.</summary>
    public static readonly WorldEvent<UnloadedChange> Applied = new WorldEvent<UnloadedChange>(nameof(Applied));

    /// <summary>A queued change was dropped: its container was gone, full, or didn't hold enough to take.</summary>
    public static readonly WorldEvent<UnloadedChange> Failed = new WorldEvent<UnloadedChange>(nameof(Failed));

    internal static List<UnloadedStorageEntry> Entries => _data != null ? _data.Value.Entries : new List<UnloadedStorageEntry>();

    internal static void Init(ObenMod framework)
    {
        _data = ModSaves.Register<UnloadedStorageFile>(framework, "unloaded-storage");
        _data.DataLoaded.Subscribe(framework, OnDataLoaded);
        OnDataLoaded(_data.Value);
        GameEvents.Saving.Subscribe(framework, DropSaved);
        UnloadedStorageApply.Init(framework);
        framework.Track(Disposable.Create(() =>
        {
            Settled.Clear();
            _data = null;
        }));
    }

    /// <summary>A container's contents from the save, or null with the reason in <paramref name="refusal"/>.</summary>
    public static UnloadedContainer Read(string scene, string guid, out string refusal)
    {
        string path = ScenePath(scene, out refusal);
        if (path == null)
            return null;
        if (!UnloadedStorageRules.ValidGuid(guid))
        {
            refusal = "the container id must be 1 to 128 characters";
            return null;
        }
        try
        {
            return UnloadedStorageReader.Read(path, guid, out refusal);
        }
        catch (Exception e)
        {
            refusal = "reading the scene file failed: " + e.Message;
            return null;
        }
    }

    /// <summary>A container's contents from the save, or null (the reason is logged once).</summary>
    public static UnloadedContainer Read(string scene, string guid)
    {
        var container = Read(scene, guid, out string refusal);
        if (container == null)
            ItemsLog.WarnOnce("unloaded-read-" + scene + guid, $"UnloadedStorage.Read({scene}, {guid}): {refusal}.");
        return container;
    }

    /// <summary>The GUIDs of the containers the save has for a scene that is not loaded.</summary>
    public static List<string> ContainerIds(string scene, out string refusal)
    {
        string path = ScenePath(scene, out refusal);
        return path != null ? UnloadedStorageReader.Guids(path, out refusal) : new List<string>();
    }

    /// <summary>Queues taking items out of a container in a scene that is not loaded. False when refused (the reason is logged once).</summary>
    public static bool QueueTake(ObenMod owner, string scene, string guid, int itemId, int count) =>
        Queue(owner, scene, guid, itemId, count, give: false);

    /// <summary>Queues putting items into a container in a scene that is not loaded. False when refused (the reason is logged once).</summary>
    public static bool QueueGive(ObenMod owner, string scene, string guid, int itemId, int count) =>
        Queue(owner, scene, guid, itemId, count, give: true);

    /// <summary>Changes still waiting for their scene, of one mod or (with null) of all mods.</summary>
    public static List<UnloadedChange> Pending(string modId = null)
    {
        var list = new List<UnloadedChange>();
        foreach (var entry in Entries)
        {
            if ((modId == null || entry.Mod == modId) && !Settled.Contains(entry))
                list.Add(new UnloadedChange(entry));
        }
        return list;
    }

    /// <summary>Drops the mod's changes that haven't been applied yet. Returns how many.</summary>
    public static int Cancel(ObenMod owner)
    {
        if (owner == null || _data == null)
            return 0;
        string mod = owner.Info.Id;
        return Entries.RemoveAll(e => e.Mod == mod && !Settled.Contains(e));
    }

    internal static void Settle(UnloadedStorageEntry entry) => Settled.Add(entry);

    internal static void Drop(UnloadedStorageEntry entry)
    {
        Entries.Remove(entry);
        Settled.Remove(entry);
    }

    private static bool Queue(ObenMod owner, string scene, string guid, int itemId, int count, bool give)
    {
        string what = give ? "QueueGive" : "QueueTake";
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (_data == null)
            return Refuse(what, "the framework isn't ready");
        if (!UnloadedStorageRules.ValidItem(itemId))
            return Refuse(what, "item ids 0 and 1 are money, not items");
        if (!UnloadedStorageRules.ValidCount(count))
            return Refuse(what, $"the count must be 1 to {UnloadedStorageRules.MaxCount}");
        if (Read(scene, guid, out string refusal) == null)
            return Refuse(what, refusal);

        var entry = new UnloadedStorageEntry { Mod = owner.Info.Id, Scene = scene, Guid = guid, ItemId = itemId, Count = count, Give = give };
        if (!UnloadedStorageRules.Append(Entries, entry, Settled))
            return Refuse(what, $"a mod keeps at most {UnloadedStorageRules.MaxEntriesPerMod} queued changes");
        return true;
    }

    private static string ScenePath(string scene, out string refusal)
    {
        refusal = null;
        if (!UnloadedStorageRules.ValidScene(scene))
            refusal = "the scene must be a scene name of 1 to 128 characters";
        else if (SaveGate.IsBusy)
            refusal = "a save is loading or being written";
        else if (SceneManager.GetSceneByName(scene).isLoaded)
            refusal = "the scene is loaded; use Containers for it";
        else if (SaveFolders.LoadedFolder() == null)
            refusal = "no save is loaded";
        return refusal == null ? Path.Combine(SaveFolders.LoadedFolder(), scene + ".tnmt") : null;
    }

    private static bool Refuse(string what, string reason)
    {
        ItemsLog.WarnOnce("unloaded-" + what + reason, $"UnloadedStorage.{what} refused: {reason}.");
        return false;
    }

    private static void OnDataLoaded(UnloadedStorageFile file)
    {
        Settled.Clear();
        file.Entries = UnloadedStorageRules.Clean(file).Entries;
    }

    // The scene being saved now holds the applied changes, so the queue written with this save drops them.
    private static void DropSaved()
    {
        if (_data == null)
            return;
        string active = SceneManager.GetActiveScene().name;
        Entries.RemoveAll(e => UnloadedStorageRules.Durable(e, Settled.Contains(e), active));
        Settled.RemoveWhere(e => !Entries.Contains(e));
    }
}
