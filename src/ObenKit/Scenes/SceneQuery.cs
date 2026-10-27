using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ObenKit.Scenes;

/// <summary>
/// Addressing GameObjects from text, used by tools and the dev bridge.
/// A target is either "#12345" (instance id) or a path like "Root/Child/Grandchild".
/// A bare name that isn't a root falls back to a search of the whole scene.
/// </summary>
public static class SceneQuery
{
    private static readonly Func<int, Object> FromInstanceId = CreateIdLookup();

    public static GameObject Resolve(string target)
    {
        if (string.IsNullOrEmpty(target))
            throw new ArgumentException("No target given.");

        if (target[0] == '#' && int.TryParse(target.Substring(1), out int id))
        {
            return FromInstanceId(id) switch
            {
                GameObject go => go,
                Component c => c.gameObject,
                _ => throw new ArgumentException($"Nothing with id {target}."),
            };
        }

        return ByPath(target) ?? FirstNamed(target) ?? throw new ArgumentException($"No object at '{target}'.");
    }

    /// <summary>Roots of every loaded scene plus the DontDestroyOnLoad scene.</summary>
    public static List<GameObject> Roots()
    {
        var roots = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                roots.AddRange(scene.GetRootGameObjects());
        }
        roots.AddRange(DontDestroyRoots());
        return roots;
    }

    public static string PathOf(GameObject go)
    {
        var sb = new StringBuilder(go.name);
        for (var t = go.transform.parent; t != null; t = t.parent)
            sb.Insert(0, t.name + "/");
        return sb.ToString();
    }

    /// <summary>Finds objects whose name contains <paramref name="name"/> and/or that carry a component type.</summary>
    public static List<GameObject> Find(string name, Type component, bool includeInactive, int max)
    {
        var result = new List<GameObject>();
        IEnumerable<GameObject> source = component != null
            ? ComponentsOf(component, includeInactive)
            : AllObjects(includeInactive);

        foreach (var go in source)
        {
            if (name != null && go.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            result.Add(go);
            if (result.Count >= max)
                break;
        }
        return result;
    }

    private static IEnumerable<GameObject> ComponentsOf(Type component, bool includeInactive)
    {
        foreach (var c in Object.FindObjectsByType(component, Inactive(includeInactive), FindObjectsSortMode.None))
        {
            if (c is Component comp)
                yield return comp.gameObject;
        }
    }

    private static IEnumerable<GameObject> AllObjects(bool includeInactive)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(Inactive(includeInactive), FindObjectsSortMode.None))
            yield return t.gameObject;
    }

    private static FindObjectsInactive Inactive(bool include) =>
        include ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;

    private static GameObject ByPath(string path)
    {
        string[] parts = path.Split('/');
        foreach (var root in Roots())
        {
            if (root.name != parts[0])
                continue;
            var t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++)
                t = t.Find(parts[i]);
            if (t != null)
                return t.gameObject;
        }
        return null;
    }

    private static GameObject FirstNamed(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == name)
                return t.gameObject;
        }
        return null;
    }

    /// <summary>Unity's internal id lookup when available, otherwise a (slow) scan.</summary>
    private static Func<int, Object> CreateIdLookup()
    {
        var method = AccessTools.Method(typeof(Object), "FindObjectFromInstanceID");
        if (method != null)
            return AccessTools.MethodDelegate<Func<int, Object>>(method);

        return id =>
        {
            foreach (var o in Object.FindObjectsByType<Object>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (o.GetInstanceID() == id)
                    return o;
            }
            return null;
        };
    }

    private static IEnumerable<GameObject> DontDestroyRoots()
    {
        // The DontDestroyOnLoad scene can only be reached through an object that lives in it.
        var probe = new GameObject("ObenKit.SceneProbe");
        Object.DontDestroyOnLoad(probe);
        var roots = probe.scene.GetRootGameObjects();
        Object.DestroyImmediate(probe);
        foreach (var go in roots)
        {
            if (go != probe)
                yield return go;
        }
    }
}
