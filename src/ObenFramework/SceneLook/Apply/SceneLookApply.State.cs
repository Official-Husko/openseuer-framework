// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.SceneLook;

internal static partial class SceneLookApply
{
    // Keyed by instance id: destroyed GameObjects compare equal to each other, so they make poor keys.
    private static readonly Dictionary<int, SceneLookApplied> Bodies = new Dictionary<int, SceneLookApplied>();
    private static readonly Dictionary<SceneLookLook, SceneLookAssets> Built = new Dictionary<SceneLookLook, SceneLookAssets>();

    public static int Count => Bodies.Count;

    /// <summary>The applied look on a body, or null.</summary>
    public static SceneLookApplied State(GameObject body)
    {
        Prune();
        return body != null && Bodies.TryGetValue(body.GetInstanceID(), out var state) ? state : null;
    }

    /// <summary>Every body wearing a look right now.</summary>
    public static List<SceneLookApplied> All()
    {
        Prune();
        return new List<SceneLookApplied>(Bodies.Values);
    }

    /// <summary>A body wearing <paramref name="look"/>, or null.</summary>
    public static GameObject BodyOf(SceneLookLook look)
    {
        Prune();
        foreach (var state in Bodies.Values)
        {
            if (state.Look == look)
                return state.Body;
        }
        return null;
    }

    public static bool Driver(string modId, GameObject body, out SceneLookPoseDriver driver, out string reason)
    {
        driver = null;
        var state = State(body);
        reason = state == null ? "the body has no applied look"
            : state.ModId != modId ? "the look on this body was applied by mod " + state.ModId
            : state.Driver == null ? "the look on this body has no pose driver (only retargeted looks have one)"
            : null;
        if (reason != null)
            return false;
        driver = state.Driver;
        return true;
    }

    /// <summary>Takes the look off a body, if <paramref name="modId"/> put it there.</summary>
    public static bool Remove(string modId, GameObject body, out string reason)
    {
        var state = State(body);
        reason = body == null ? "no body"
            : state == null ? "the body has no applied look"
            : state.ModId != modId ? "the look on this body was applied by mod " + state.ModId
            : null;
        if (reason != null)
            return false;
        Bodies.Remove(body.GetInstanceID());
        Finish(state);
        return true;
    }

    /// <summary>Takes off every look a mod applied. Returns how many.</summary>
    public static int RemoveMod(string modId)
    {
        int count = 0;
        foreach (var key in new List<int>(Bodies.Keys))
        {
            var state = Bodies[key];
            if (state.ModId != modId)
                continue;
            Bodies.Remove(key);
            Finish(state);
            count++;
        }
        return count;
    }

    /// <summary>Takes every look off and frees everything built. Used when the framework unloads.</summary>
    public static void RemoveAll()
    {
        foreach (var state in new List<SceneLookApplied>(Bodies.Values))
        {
            try { Undo(state); }
            catch (Exception e) { SceneLooks.Log.Error("remove all", e); }
        }
        Bodies.Clear();
        foreach (var pair in new List<KeyValuePair<SceneLookLook, SceneLookAssets>>(Built))
            Forget(pair.Key, pair.Value);
        Built.Clear();
    }

    /// <summary>Frees a look's textures and materials once no body wears it.</summary>
    public static void Release(SceneLookLook look)
    {
        Prune();
        if (look != null && Built.TryGetValue(look, out var assets) && assets.Bodies <= 0)
            Forget(look, assets);
    }

    public static void BodyGone(GameObject body)
    {
        try
        {
            if (ReferenceEquals(body, null) || !Bodies.TryGetValue(body.GetInstanceID(), out var state))
                return;
            Bodies.Remove(body.GetInstanceID());
            Finish(state);
        }
        catch (Exception e)
        {
            SceneLooks.Log.Error("body gone", e);
        }
    }

    private static void Prune()
    {
        List<int> dead = null;
        foreach (var pair in Bodies)
        {
            if (pair.Value.Body == null)
                (dead ??= new List<int>()).Add(pair.Key);
        }
        if (dead == null)
            return;
        foreach (int key in dead)
        {
            var state = Bodies[key];
            Bodies.Remove(key);
            Finish(state);
        }
    }

    private static void Finish(SceneLookApplied state)
    {
        Undo(state);
        if (state.Look != null && Built.TryGetValue(state.Look, out var assets))
        {
            assets.Bodies--;
            if (assets.Bodies <= 0)
                Forget(state.Look, assets);
        }
    }

    private static SceneLookAssets Assets(SceneLookLook look)
    {
        if (Built.TryGetValue(look, out var assets))
            return assets;
        assets = new SceneLookAssets { Files = look.Files, Look = look };
        Built[look] = assets;
        return assets;
    }

    private static void Forget(SceneLookLook look, SceneLookAssets assets)
    {
        Built.Remove(look);
        foreach (var texture in assets.Textures.Values)
        {
            if (texture != null)
                Object.Destroy(texture);
        }
        foreach (var material in assets.Materials.Values)
        {
            if (material != null)
                Object.Destroy(material);
        }
        assets.Textures.Clear();
        assets.Materials.Clear();
        assets.Bodies = 0;
    }

    // Destroys what the look made and switches the body's own renderers back to how they were.
    private static void Undo(SceneLookApplied state)
    {
        if (state == null)
            return;
        foreach (var created in state.Objects)
        {
            if (created != null)
                Object.Destroy(created);
        }
        state.Objects.Clear();
        foreach (var mesh in state.Meshes)
        {
            if (mesh != null)
                Object.Destroy(mesh);
        }
        state.Meshes.Clear();
        foreach (var hidden in state.Hidden)
        {
            if (hidden.Renderer != null)
                hidden.Renderer.enabled = hidden.Enabled;
        }
        state.Hidden.Clear();
        if (state.Avatar != null)
            Object.Destroy(state.Avatar);
        state.Avatar = null;
        state.Driver = null;
        state.Holder = null;
    }
}
