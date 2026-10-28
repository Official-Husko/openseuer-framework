using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using ObenFramework.Scenes;

namespace ObenFramework.World.Addressing;

/// <summary>
/// Turns components into <see cref="ObjectAddress"/>es and back, and understands the looser texts people
/// type: a bare GUID, a hierarchy path or <c>#instanceId</c>. Everything here walks hierarchies, so it is
/// for occasional calls (a command, a message), never for per-frame use.
/// </summary>
public static class ObjectResolver
{
    private const string DontDestroyScene = "DontDestroyOnLoad";

    public static ObjectAddress AddressOf(Component component)
    {
        if (component == null)
            return default;

        var go = component.gameObject;
        return new ObjectAddress(go.scene.name, component.GetType().Name, GuidIndex.GuidOf(component), PathOf(go.transform));
    }

    /// <summary>Path of a transform with <c>#n</c> after segments that have an earlier sibling of the same name.</summary>
    public static string PathOf(Transform transform)
    {
        var segments = new List<string>();
        for (var t = transform; t != null; t = t.parent)
        {
            int index = SiblingIndex(t);
            string name = ObjectAddress.Escape(t.name);
            segments.Add(index > 0 ? name + "#" + index : name);
        }

        var sb = new StringBuilder();
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            sb.Append(segments[i]);
            if (i > 0)
                sb.Append('/');
        }
        return sb.ToString();
    }

    public static Component Resolve(ObjectAddress address)
    {
        if (address.IsEmpty)
            return null;

        if (address.Guid != null)
        {
            var byGuid = GuidIndex.Find(address.Guid);
            if (byGuid != null && TypeMatches(byGuid, address.Type) && SceneMatches(byGuid, address.Scene))
                return byGuid;
        }

        var go = WalkPath(address.Scene, address.Path);
        return go == null ? null : ComponentOf(go, address.Type);
    }

    /// <summary>
    /// Resolves any of: the full address text, a bare GUID, a hierarchy path (as <see cref="SceneQuery"/>
    /// reads it) or <c>#instanceId</c>. Returns null when nothing matches or the match isn't a <typeparamref name="T"/>.
    /// </summary>
    public static T Find<T>(string text) where T : Component
    {
        if (string.IsNullOrEmpty(text))
            return null;

        if (ObjectAddress.TryParse(text, out var address))
            return address.Resolve() as T;

        if (GuidIndex.Find(text) is T byGuid)
            return byGuid;

        try
        {
            var go = SceneQuery.Resolve(text);
            return go != null ? go.GetComponent<T>() : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool TypeMatches(Component component, string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return true;
        for (var t = component.GetType(); t != null; t = t.BaseType)
        {
            if (t.Name == typeName)
                return true;
        }
        return false;
    }

    private static bool SceneMatches(Component component, string scene) =>
        string.IsNullOrEmpty(scene) || component.gameObject.scene.name == scene;

    private static Component ComponentOf(GameObject go, string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return go.transform;
        foreach (var c in go.GetComponents<Component>())
        {
            if (c != null && TypeMatches(c, typeName))
                return c;
        }
        return null;
    }

    private static int SiblingIndex(Transform t)
    {
        int index = 0;
        if (t.parent != null)
        {
            var parent = t.parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (sibling == t)
                    break;
                if (sibling.name == t.name)
                    index++;
            }
            return index;
        }

        foreach (var root in RootsOf(t.gameObject.scene.name))
        {
            if (root.transform == t)
                break;
            if (root.name == t.name)
                index++;
        }
        return index;
    }

    private static GameObject WalkPath(string scene, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string[] segments = path.Split('/');
        Transform current = null;
        for (int i = 0; i < segments.Length; i++)
        {
            ParseSegment(segments[i], out string name, out int wanted);
            if (i == 0)
            {
                foreach (var root in RootsOf(scene))
                {
                    if (root.name == name && wanted-- == 0)
                    {
                        current = root.transform;
                        break;
                    }
                }
            }
            else
            {
                var parent = current;
                current = null;
                for (int c = 0; c < parent.childCount; c++)
                {
                    var child = parent.GetChild(c);
                    if (child.name == name && wanted-- == 0)
                    {
                        current = child;
                        break;
                    }
                }
            }

            if (current == null)
                return null;
        }
        return current.gameObject;
    }

    private static void ParseSegment(string segment, out string name, out int index)
    {
        index = 0;
        int hash = segment.LastIndexOf('#');
        if (hash > 0 && int.TryParse(segment.Substring(hash + 1), out int parsed))
        {
            index = parsed;
            segment = segment.Substring(0, hash);
        }
        name = ObjectAddress.Unescape(segment);
    }

    private static IEnumerable<GameObject> RootsOf(string sceneName)
    {
        if (sceneName == DontDestroyScene)
        {
            foreach (var root in SceneQuery.Roots())
            {
                if (root.scene.name == DontDestroyScene)
                    yield return root;
            }
            yield break;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || (!string.IsNullOrEmpty(sceneName) && scene.name != sceneName))
                continue;
            foreach (var root in scene.GetRootGameObjects())
                yield return root;
        }
    }
}
