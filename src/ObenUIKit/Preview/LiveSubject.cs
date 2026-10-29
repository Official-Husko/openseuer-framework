// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
// The approach (preview the live rig in place on a hidden layer, give everything back after) is its ModelPreview's.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenUIKit.Preview;

/// <summary>
/// A live object a model preview shows where it stands (the player, an NPC in the scene): its renderers move to the
/// preview layer and are switched on, so the preview camera sees them and the game's cameras don't, and the game
/// keeps animating them. Inactive rigs it needs are switched on (the game turns the player's off in first person),
/// and the scene's lights stop lighting the preview layer. Everything is put back exactly as it was on
/// <see cref="Dispose"/>. Only one exists at a time; a new one gives the last one's object back first.
/// </summary>
internal sealed class LiveSubject : IDisposable
{
    private sealed class Claimed
    {
        public Renderer Renderer;
        public bool Enabled;
        public int Layer;
        public bool Offscreen;
        public Bounds LocalBounds;
        public Mesh Mesh;
    }

    private static LiveSubject _current;

    private readonly Transform _facing;
    private readonly GameObject _root;
    private readonly int _layer;
    private readonly List<Claimed> _claimed = new List<Claimed>();
    private readonly HashSet<Renderer> _known = new HashSet<Renderer>();
    private readonly List<KeyValuePair<GameObject, bool>> _activated = new List<KeyValuePair<GameObject, bool>>();
    private readonly List<KeyValuePair<Light, int>> _lights = new List<KeyValuePair<Light, int>>();
    private bool _disposed;

    // The renderers under root that are active once the given objects are switched on; parts the game keeps
    // inactive (unchosen outfit slots, an unused model) are left alone.
    private LiveSubject(GameObject root, GameObject[] switchOn, int layer)
    {
        _current?.Dispose();
        _current = this;
        _facing = root.transform;
        _root = root;
        _layer = layer;
        foreach (var target in switchOn)
            Activate(target);
        Adopt();
        MaskLights();
    }

    /// <summary>
    /// The player's body, or null outside a level. In first person the game switches the two rigs off and the
    /// body's renderers (which sit beside the rigs) off as well, and some levels switch off the object holding
    /// them; all of it comes on for the preview.
    /// </summary>
    public static LiveSubject Player(int layer)
    {
        var locator = PlayerLocator.instance;
        if (locator == null || locator.playerRig == null)
            return null;
        // Some levels also switch off the object holding the worn meshes, above the rigs.
        var switchOn = new List<GameObject>();
        for (var t = locator.playerRig.transform.parent; t != null && t != locator.transform; t = t.parent)
            switchOn.Insert(0, t.gameObject);
        switchOn.Add(locator.playerRig);
        if (locator.playerRigUnrigified != null)
            switchOn.Add(locator.playerRigUnrigified);
        return new LiveSubject(locator.gameObject, switchOn.ToArray(), layer);
    }

    /// <summary>Any object in the scene, faced from its own forward.</summary>
    public static LiveSubject Of(GameObject root, int layer) =>
        root == null ? null : new LiveSubject(root, new[] { root }, layer);

    /// <summary>The object is still there.</summary>
    public bool Alive => !_disposed && _facing != null;

    /// <summary>Its forward on the ground, which the preview's turning starts from.</summary>
    public Vector3 Forward
    {
        get
        {
            var forward = _facing != null ? Vector3.ProjectOnPlane(_facing.forward, Vector3.up) : Vector3.forward;
            return forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;
        }
    }

    /// <summary>How many renderers the preview shows (the claimed ones that are on).</summary>
    public int Count
    {
        get
        {
            int count = 0;
            foreach (var claimed in _claimed)
            {
                if (claimed.Renderer != null && claimed.Renderer.enabled && claimed.Renderer.gameObject.activeInHierarchy)
                    count++;
            }
            return count;
        }
    }

    /// <summary>Picks up renderers added since (a changed outfit) and claims them too.</summary>
    public void Refresh()
    {
        if (!Alive)
            return;
        Adopt();
        foreach (var claimed in _claimed)
            Claim(claimed.Renderer);
    }

    /// <summary>The world bounds of what is visible now, or false when nothing is.</summary>
    public bool Measure(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var claimed in _claimed)
        {
            var renderer = claimed.Renderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            var b = renderer.bounds;
            if (b.size.sqrMagnitude < 1e-8f)
                continue;
            if (any)
                bounds.Encapsulate(b);
            else
                bounds = b;
            any = true;
        }
        return any;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_current == this)
            _current = null;
        foreach (var claimed in _claimed)
            Restore(claimed);
        foreach (var pair in _activated)
        {
            if (pair.Key != null && pair.Key.activeSelf != pair.Value)
                pair.Key.SetActive(pair.Value);
        }
        foreach (var pair in _lights)
        {
            if (pair.Key != null)
                pair.Key.cullingMask = pair.Value;
        }
        _claimed.Clear();
        _activated.Clear();
        _lights.Clear();
    }

    private void Activate(GameObject root)
    {
        if (root == null)
            return;
        _activated.Add(new KeyValuePair<GameObject, bool>(root, root.activeSelf));
        if (!root.activeSelf)
            root.SetActive(true);
    }

    private void Adopt()
    {
        if (_root == null)
            return;
        foreach (var renderer in _root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer) || !_known.Add(renderer))
                continue;
            var claimed = new Claimed { Renderer = renderer, Enabled = renderer.enabled, Layer = renderer.gameObject.layer };
            if (renderer is SkinnedMeshRenderer skin)
            {
                claimed.Offscreen = skin.updateWhenOffscreen;
                claimed.LocalBounds = skin.localBounds;
                claimed.Mesh = skin.sharedMesh;
            }
            _claimed.Add(claimed);
            Claim(renderer);
        }
    }

    private void Claim(Renderer renderer)
    {
        // Parts the game keeps switched off (a hidden hat slot) stay hidden.
        if (renderer == null || !renderer.gameObject.activeInHierarchy)
            return;
        renderer.enabled = true;
        renderer.gameObject.layer = _layer;
        if (renderer is SkinnedMeshRenderer skin)
        {
            skin.updateWhenOffscreen = true;
            if (skin.sharedMesh != null)
                skin.localBounds = skin.sharedMesh.bounds;
        }
    }

    private static void Restore(Claimed claimed)
    {
        var renderer = claimed.Renderer;
        if (renderer == null)
            return;
        renderer.enabled = claimed.Enabled;
        renderer.gameObject.layer = claimed.Layer;
        if (renderer is SkinnedMeshRenderer skin)
        {
            skin.updateWhenOffscreen = claimed.Offscreen;
            // A new mesh brought its own bounds; only the one the preview changed gets its old ones back.
            if (skin.sharedMesh == claimed.Mesh)
                skin.localBounds = claimed.LocalBounds;
        }
    }

    // Lamps, lanterns and the sun would light the subject from wherever they are in the scene.
    private void MaskLights()
    {
        int bit = 1 << _layer;
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if ((light.cullingMask & bit) == 0 || light.transform.root.name == ModelStage.RootName)
                continue;
            _lights.Add(new KeyValuePair<Light, int>(light, light.cullingMask));
            light.cullingMask &= ~bit;
        }
    }
}
