// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ObenFramework.World.Props;

public static partial class WorldProps
{
    private static readonly Dictionary<string, GameObject> Live = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, Action<WorldPropUse>> Handlers = new Dictionary<string, Action<WorldPropUse>>();
    private static ModSaveData<PropFile> _save;

    private static PropFile Store => _save.Value;

    /// <summary>
    /// Registers what happens when the player uses a prop of this mod placed with
    /// <see cref="WorldPropOptions.InteractKey"/> = <paramref name="key"/>. Props already in the world get
    /// their prompt right away. The handler is removed when the owner unloads. Throws when the mod already
    /// has a handler for the key.
    /// </summary>
    public static void OnUse(ObenMod owner, string key, Action<WorldPropUse> handler)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (string.IsNullOrEmpty(key) || key.Length > PropRules.MaxInteractKeyLength)
            throw new ArgumentException("key must be 1 to 64 characters", nameof(key));
        string id = Key(owner.Info.Id, key);
        if (Handlers.ContainsKey(id))
            throw new ArgumentException($"{owner.Info.Id} already handles the prop key '{key}'.", nameof(key));

        Handlers[id] = handler;
        owner.Track(Disposable.Create(() => Handlers.Remove(id)));
        if (_save == null)
            return;
        foreach (var record in Store.Props)
        {
            var live = LiveObject(record.Mod, record.Id);
            if (live != null && record.Mod == owner.Info.Id && record.InteractKey == key)
                AttachUse(record, live);
        }
    }

    /// <summary>How many props are in the world right now.</summary>
    public static int LiveCount
    {
        get
        {
            int count = 0;
            foreach (var go in Live.Values)
            {
                if (go != null)
                    count++;
            }
            return count;
        }
    }

    internal static void Init(ObenMod framework)
    {
        _save = ModSaves.Register<PropFile>(framework, "props");
        _save.DataLoaded.Subscribe(framework, _ => OnDataLoaded());
        OnDataLoaded();
        framework.Track(Disposable.Create(() =>
        {
            DespawnAll();
            Handlers.Clear();
            _save = null;
            LastProblem = null;
            Log.Reset();
        }));
    }

    /// <summary>
    /// Builds every prop of <paramref name="scene"/> that isn't in the world yet. Returns how many wait for a
    /// model, -1 when the game is busy saving or loading (try again later), 0 when nothing is left to do.
    /// </summary>
    internal static int Restore(string scene)
    {
        if (_save == null || !SceneManager.GetSceneByName(scene).isLoaded)
            return 0;
        if (SaveGate.IsBusy)
            return -1;
        int waiting = 0;
        foreach (var record in PropRules.InScene(Store, scene))
        {
            if (LiveObject(record.Mod, record.Id) != null || !ModRegistry.IsLoaded(record.Mod))
                continue;
            try
            {
                if (Spawn(record) == null && record.Kind == WorldPropKind.Model)
                    waiting++;
            }
            catch (Exception e)
            {
                Log.Error("restoring " + record.Id, e);
            }
        }
        return waiting;
    }

    /// <summary>Takes every prop of a mod out of the world, plus the props built from that mod's models.</summary>
    internal static void DespawnMod(string modId)
    {
        if (_save == null)
            return;
        foreach (var record in Store.Props)
        {
            if (record.Mod == modId || (record.Kind == WorldPropKind.Model && PropRules.SplitModelKey(record.Key, out string modelMod, out _) && modelMod == modId))
                Despawn(record.Mod, record.Id);
        }
    }

    /// <summary>Forgets every live prop; the scene they were in is going away and destroys them.</summary>
    internal static void DespawnAll()
    {
        foreach (var go in Live.Values)
        {
            if (go != null)
                Object.Destroy(go);
        }
        Live.Clear();
    }

    private static GameObject Spawn(PropRecord record)
    {
        var existing = LiveObject(record.Mod, record.Id);
        if (existing != null)
            return existing;
        if (LiveCount >= PropRules.MaxLive)
            return Refuse<GameObject>("live cap", $"at most {PropRules.MaxLive} props are in the world at once");

        var go = PropBuilder.Build(record, out string reason);
        if (go == null)
            return Refuse<GameObject>($"build {record.Mod}/{record.Id}", reason ?? "the prop could not be built");
        Live[Key(record.Mod, record.Id)] = go;
        if (!string.IsNullOrEmpty(record.InteractKey))
            AttachUse(record, go);
        Spawned.Raise(new WorldPropInfo(record, go));
        return go;
    }

    private static void Despawn(string mod, string id)
    {
        string key = Key(mod, id);
        if (!Live.TryGetValue(key, out var go))
            return;
        Live.Remove(key);
        if (go != null)
        {
            go.SetActive(false);
            Object.Destroy(go);
        }
    }

    private static void AttachUse(PropRecord record, GameObject go)
    {
        if (!Handlers.TryGetValue(Key(record.Mod, record.InteractKey), out var handler))
            return;
        string title = string.IsNullOrEmpty(record.Title) ? record.Id : record.Title;
        string mod = record.Mod, id = record.Id;
        if (!PropUse.Attach(go, title, () => Use(mod, id, handler)))
            Log.Warn("use " + record.Id, "the use prompt couldn't be added (no collider on a layer the game can target)");
    }

    private static void Use(string mod, string id, Action<WorldPropUse> handler)
    {
        var record = _save != null ? PropRules.Find(Store, mod, id) : null;
        if (record == null)
            return;
        try { handler(new WorldPropUse(record, LiveObject(mod, id))); }
        catch (Exception e) { Log.Error($"use handler of {mod}/{id}", e); }
    }

    private static void OnDataLoaded()
    {
        DespawnAll();
        var clean = PropRules.Clean(Store);
        Store.Props.Clear();
        Store.Props.AddRange(clean.Props);
    }

    private static GameObject LiveObject(string mod, string id)
    {
        string key = Key(mod, id);
        if (!Live.TryGetValue(key, out var go))
            return null;
        if (go != null)
            return go;
        Live.Remove(key);
        return null;
    }

    private static string Key(string mod, string id) => mod + "\n" + id;
}
