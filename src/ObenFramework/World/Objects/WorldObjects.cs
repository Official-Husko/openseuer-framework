// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenFramework.Scenes;
using ObenFramework.World.Addressing;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.World;

/// <summary>What kind of saved object something is, as far as mods usually care.</summary>
public enum WorldObjectKind
{
    Other,
    Collectible,
    Item,
    Package,
    Coin,
}

/// <summary>A scene object the game saves (it has a GUID or was spawned from a saved prefab).</summary>
public sealed class WorldObjectInfo
{
    internal WorldObjectInfo(string guid, GameObject go, WorldObjectKind kind)
    {
        Guid = guid;
        Name = go.name;
        Kind = kind;
        Position = go.transform.position;
        Scene = go.scene.name;
        Object = go;
    }

    /// <summary>The game's save GUID, or null for an object spawned from a saved prefab without one.</summary>
    public string Guid { get; }

    public string Name { get; }
    public WorldObjectKind Kind { get; }
    public Vector3 Position { get; }
    public string Scene { get; }
    public GameObject Object { get; }
}

/// <summary>
/// The game's saved scene objects: find them by GUID or hierarchy path, list those near a point, and let
/// mods keep a bit of text with any of them (kept in the framework's save file, per mod, with a budget).
/// </summary>
public static class WorldObjects
{
    private static readonly ObjectDataStore Data = new ObjectDataStore();
    private static ModSaveData<ObjectDataFile> _save;

    /// <summary>The object whose saved script has this GUID, or null.</summary>
    public static GameObject Find(string guid)
    {
        var found = GuidIndex.Find(guid);
        return found != null ? found.gameObject : null;
    }

    /// <summary>
    /// The object at a plain hierarchy path (<c>Root/Child/Grandchild</c>) in a loaded scene. Siblings with
    /// the same name are all tried. Returns null when nothing matches or the scene isn't loaded.
    /// </summary>
    public static GameObject FindByPath(string scene, string path, bool includeInactive = false)
    {
        if (string.IsNullOrEmpty(scene)) throw new ArgumentException("scene must not be empty", nameof(scene));
        var names = ObjectPath.Split(path) ?? throw new ArgumentException("path must be names separated by '/', none of them empty", nameof(path));
        var loaded = SceneManager.GetSceneByName(scene);
        if (!loaded.isLoaded)
            return null;
        var roots = new List<Transform>();
        foreach (var root in loaded.GetRootGameObjects())
            roots.Add(root.transform);
        var found = ObjectPath.Descend(roots, names, 0, t => t.name, t => includeInactive || t.gameObject.activeInHierarchy, ChildrenOf);
        return found != null ? found.gameObject : null;
    }

    /// <summary>The plain hierarchy path of an object, as <see cref="FindByPath"/> takes it.</summary>
    public static string PathOf(GameObject go)
    {
        if (go == null) throw new ArgumentNullException(nameof(go));
        return SceneQuery.PathOf(go);
    }

    /// <summary>The GUID of the first saved script on the object, or null.</summary>
    public static string GuidOf(GameObject go)
    {
        if (go == null)
            return null;
        foreach (var script in go.GetComponents<SavableScript>())
        {
            string guid = GuidIndex.GuidOf(script);
            if (guid != null)
                return guid;
        }
        return null;
    }

    /// <summary>The game saves this object: it has a GUID, or it was spawned from a prefab the save records.</summary>
    public static bool IsSaved(GameObject go) =>
        go != null && go.activeInHierarchy && (GuidOf(go) != null || go.GetComponent<InstantiatedPrefabRef>() != null);

    public static WorldObjectInfo InfoOf(GameObject go) =>
        IsSaved(go) ? new WorldObjectInfo(GuidOf(go), go, KindOf(go)) : null;

    /// <summary>Saved objects of the active scene within <paramref name="radius"/> metres. Scans the scene, so not per frame.</summary>
    public static List<WorldObjectInfo> Near(Vector3 position, float radius)
    {
        if (!(radius >= 0f)) throw new ArgumentException("radius must not be negative", nameof(radius));
        var result = new List<WorldObjectInfo>();
        var seen = new HashSet<GameObject>();
        float max = radius * radius;
        foreach (var script in SceneIds.Active<SavableScript>())
        {
            if ((script.transform.position - position).sqrMagnitude > max || !seen.Add(script.gameObject))
                continue;
            var info = InfoOf(script.gameObject);
            if (info != null)
                result.Add(info);
        }
        return result;
    }

    /// <summary>The text <paramref name="owner"/> keeps with the object, or null.</summary>
    public static string GetData(ObenMod owner, WorldObjectInfo obj)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (obj == null || string.IsNullOrEmpty(obj.Guid) || WorldStateLoading())
            return null;
        return Data.Get(owner.Info.Id, obj.Scene, obj.Guid);
    }

    /// <summary>
    /// Keeps text with a saved object for this save (null removes it). Up to 16384 characters each and
    /// 256 KB per mod in total. Objects without a GUID can't hold data; placed furniture gets a new GUID on
    /// every save, so it can't either.
    /// </summary>
    public static bool SetData(ObenMod owner, WorldObjectInfo obj, string blob)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (_save == null || SaveGate.IsBusy || obj == null || string.IsNullOrEmpty(obj.Guid))
            return false;
        if (blob == null)
            return Data.Remove(owner.Info.Id, obj.Scene, obj.Guid);
        return Data.Put(owner.Info.Id, obj.Scene, obj.Guid, blob);
    }

    internal static void Init(ObenMod framework)
    {
        _save = ModSaves.Register<ObjectDataFile>(framework, "objectdata");
        // Tracked after the registration, so it runs first on unload and the handed-over data is current.
        framework.Track(Disposable.Create(() =>
        {
            if (_save != null)
                Data.SaveTo(_save.Value);
            Data.Clear();
            _save = null;
        }));
        _save.DataLoaded.Subscribe(framework, file => Data.Load(file));
        GameEvents.Saving.Subscribe(framework, () => Data.SaveTo(_save.Value));
        Data.Load(_save.Value);
    }

    private static bool WorldStateLoading() => Game.WorldState.IsLoadingSave;

    private static WorldObjectKind KindOf(GameObject go)
    {
        if (go.GetComponent<CollectibleItem>() != null) return WorldObjectKind.Item;
        if (go.GetComponent<CollectiblePackage>() != null) return WorldObjectKind.Package;
        if (go.GetComponent<CollectibleCoin>() != null) return WorldObjectKind.Coin;
        if (go.GetComponent<Collectible>() != null) return WorldObjectKind.Collectible;
        return WorldObjectKind.Other;
    }

    private static IEnumerable<Transform> ChildrenOf(Transform node)
    {
        for (int i = 0; i < node.childCount; i++)
            yield return node.GetChild(i);
    }
}
