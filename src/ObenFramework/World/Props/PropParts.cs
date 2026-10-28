// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World.Props;

/// <summary>Turns a copy of a game prefab into inert decoration: no scripts, no physics, no sound.</summary>
internal static class PropParts
{
    /// <summary>
    /// Removes every script. Some scripts require others, so it goes round until nothing more can be
    /// removed. Returns false when a script survives, in which case the copy must not be used.
    /// </summary>
    public static bool Strip(GameObject go)
    {
        int before = ScriptCount(go);
        while (before > 0)
        {
            var parts = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (parts[i] == null)
                    continue;
                try { Object.DestroyImmediate(parts[i]); }
                catch (Exception) { }
            }
            int after = ScriptCount(go);
            if (after == 0 || after >= before)
                break;
            before = after;
        }
        foreach (var animator in go.GetComponentsInChildren<Animator>(true))
            animator.fireEvents = false;
        return ScriptCount(go) == 0;
    }

    /// <summary>Stops bodies falling, sounds playing and particles spawning.</summary>
    public static void Settle(GameObject go)
    {
        foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
        {
            source.playOnAwake = false;
            source.Stop();
        }
        foreach (var system in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = system.main;
            main.playOnAwake = false;
            system.Stop();
        }
    }

    /// <summary>Adds a box around the renderer when the object has no collider anywhere.</summary>
    public static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>(true) != null)
            return;
        var box = go.AddComponent<BoxCollider>();
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
        {
            box.size = Vector3.one;
            return;
        }
        box.center = renderer.localBounds.center;
        box.size = Vector3.Max(renderer.localBounds.size, Vector3.one * 0.01f);
    }

    public static void RemoveColliders(GameObject go)
    {
        foreach (var collider in go.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
    }

    private static int ScriptCount(GameObject go)
    {
        int count = 0;
        foreach (var part in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (part != null)
                count++;
        }
        return count;
    }
}

/// <summary>
/// An inactive object that copies of game prefabs are made under, so their scripts don't wake up before
/// they are stripped. Destroyed when the framework unloads.
/// </summary>
internal static class PropHolder
{
    private static GameObject _holder;

    public static Transform Transform
    {
        get
        {
            if (_holder == null)
            {
                _holder = new GameObject("ObenFramework.PropHolder");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            return _holder.transform;
        }
    }

    public static void Destroy()
    {
        if (_holder != null)
            Object.Destroy(_holder);
        _holder = null;
    }
}
