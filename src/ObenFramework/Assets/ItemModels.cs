// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenLoader.Runtime;
using OS.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Assets;

/// <summary>
/// Shows a mod's model for an item, the game's or a mod's: on the item lying in the world (pickups, also ones
/// loaded from a save) and in the player's hands. The model replaces the look only; the pickup keeps its
/// colliders and behaviour, the held item its animations. Sized to the original by default. Lasts until the
/// mod unloads; pickups already in the world keep the model until the level loads again.
/// </summary>
public static class ItemModels
{
    internal const string PartName = "Mod model";

    private sealed class Entry
    {
        public int ItemId;
        public ModelTemplate Model;
        public bool Fit;
        public bool Held;
        public GameObject HeldPrefab;
        public GameObject OriginalHeld;
    }

    private static readonly List<Entry> Entries = new List<Entry>();
    private static GameObject _holder;

    /// <summary>
    /// Shows <paramref name="model"/> for the item where it lies in the world until <paramref name="owner"/> unloads.
    /// With <paramref name="fit"/> the model is scaled so its longest side matches the original's.
    /// </summary>
    public static IDisposable SetWorldModel(ITracker owner, int itemId, ModelTemplate model, bool fit = true) =>
        Add(owner, new Entry { ItemId = itemId, Model = model, Fit = fit });

    /// <summary>
    /// Shows <paramref name="model"/> for the item in the player's hands (first and third person). Only for items
    /// that can be held (equipable ones). Applies the next time the item is taken in hand.
    /// </summary>
    public static IDisposable SetHeldModel(ITracker owner, int itemId, ModelTemplate model, bool fit = true)
    {
        var item = ItemLookup.ById(itemId) ?? throw new ArgumentException($"Item {itemId} doesn't exist.", nameof(itemId));
        if (!(item.GetActionOfType<ItemEquipable>() is ItemEquipable))
            throw new ArgumentException($"Item {itemId} ({item.Title}) can't be held.", nameof(itemId));
        return Add(owner, new Entry { ItemId = itemId, Model = model, Fit = fit, Held = true });
    }

    private static IDisposable Add(ITracker owner, Entry entry)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (entry.Model == null || !entry.Model.IsAlive)
            throw new ArgumentException("The model isn't loaded.");
        Entries.Add(entry);
        if (entry.Held)
            ApplyHeld(entry);
        var registration = Disposable.Create(() => Remove(entry));
        owner.Track(registration);
        return registration;
    }

    private static Entry Top(int itemId, bool held)
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (Entries[i].ItemId == itemId && Entries[i].Held == held && Entries[i].Model.IsAlive)
                return Entries[i];
        }
        return null;
    }

    // A pickup of the item started: swap its look once.
    internal static void OnPickupStart(CollectibleItem pickup)
    {
        if (Entries.Count == 0 || pickup.Item == null)
            return;
        var entry = Top(pickup.Item.ID, held: false);
        if (entry != null && pickup.transform.Find(PartName) == null)
            Swap(pickup.gameObject, entry.Model, entry.Fit);
    }

    private static void ApplyHeld(Entry entry)
    {
        var action = ItemLookup.ById(entry.ItemId)?.GetActionOfType<ItemEquipable>() as ItemEquipable;
        if (action == null)
            return;
        if (action.wieldPrefab == null)
            action.LoadPrefab();
        var original = action.wieldPrefab;
        if (original == null)
        {
            AssetsLog.Warn($"Item {entry.ItemId} has no held object to put a model on.");
            return;
        }
        var copy = Object.Instantiate(original, Holder());
        copy.name = original.name;
        var hand = copy.GetComponent<PlayerHandItems.IPlayerHandItem>();
        bool swapped = hand != null && (Swap(hand.objectFirstPerson, entry.Model, entry.Fit) | Swap(hand.objectThirdPerson, entry.Model, entry.Fit));
        if (!swapped)
        {
            Object.Destroy(copy);
            AssetsLog.Warn($"Item {entry.ItemId}'s held object has no mesh to replace; its look is kept.");
            return;
        }
        entry.OriginalHeld = original;
        entry.HeldPrefab = copy;
        action.wieldPrefab = copy;
    }

    private static void Remove(Entry entry)
    {
        if (!Entries.Remove(entry))
            return;
        if (!entry.Held)
        {
            Restore(entry.ItemId);
            return;
        }
        if (entry.HeldPrefab == null)
            return;
        var action = ItemLookup.ById(entry.ItemId)?.GetActionOfType<ItemEquipable>() as ItemEquipable;
        if (action != null && action.wieldPrefab == entry.HeldPrefab)
        {
            var next = Top(entry.ItemId, held: true);
            action.wieldPrefab = entry.OriginalHeld;
            if (next != null && next.HeldPrefab != null)
                action.wieldPrefab = next.HeldPrefab;
        }
        Object.Destroy(entry.HeldPrefab);
    }

    // The model's mesh goes with its mod: give pickups in the world their own look back (a scan, on unload only).
    private static void Restore(int itemId)
    {
        if (Top(itemId, held: false) != null)
            return;
        foreach (var pickup in Object.FindObjectsByType<CollectibleItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var part = pickup.Item != null && pickup.Item.ID == itemId ? pickup.transform.Find(PartName) : null;
            if (part == null)
                continue;
            Object.Destroy(part.gameObject);
            foreach (var renderer in pickup.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform != part)
                    renderer.enabled = true;
            }
        }
    }

    internal static void Reset()
    {
        foreach (var entry in new List<Entry>(Entries))
            Remove(entry);
        Entries.Clear();
        if (_holder != null)
            Object.Destroy(_holder);
        _holder = null;
    }

    /// <summary>Hides the renderers under <paramref name="root"/> and puts the model in their place. False when there was nothing to replace.</summary>
    private static bool Swap(GameObject root, ModelTemplate model, bool fit)
    {
        if (root == null)
            return false;
        var renderers = new List<Renderer>();
        Bounds bounds = default;
        bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (!renderer.enabled || filter == null || filter.sharedMesh == null || renderer.name == PartName)
                continue;
            var local = LocalBounds(filter.sharedMesh.bounds, renderer.transform, root.transform);
            if (found)
                bounds.Encapsulate(local);
            else
                bounds = local;
            found = true;
            renderers.Add(renderer);
        }
        if (!found)
            return false;

        var part = new GameObject(PartName);
        part.layer = renderers[0].gameObject.layer;
        part.transform.SetParent(root.transform, false);
        var modelBounds = model.Bounds;
        float scale = 1f;
        if (fit)
        {
            float wanted = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float has = Mathf.Max(modelBounds.size.x, Mathf.Max(modelBounds.size.y, modelBounds.size.z));
            if (wanted > 0f && has > 0f)
                scale = wanted / has;
        }
        part.transform.localScale = Vector3.one * scale;
        part.transform.localPosition = bounds.center - modelBounds.center * scale;
        part.AddComponent<MeshFilter>().sharedMesh = model.Mesh;
        part.AddComponent<MeshRenderer>().sharedMaterials = model.Materials;
        foreach (var renderer in renderers)
            renderer.enabled = false;
        return true;
    }

    private static Bounds LocalBounds(Bounds bounds, Transform from, Transform to)
    {
        var min = bounds.min;
        var max = bounds.max;
        var result = new Bounds(to.InverseTransformPoint(from.TransformPoint(min)), Vector3.zero);
        for (int i = 1; i < 8; i++)
            result.Encapsulate(to.InverseTransformPoint(from.TransformPoint(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z))));
        return result;
    }

    private static Transform Holder()
    {
        if (_holder == null)
        {
            _holder = new GameObject("ObenFramework held models");
            _holder.SetActive(false);
            Object.DontDestroyOnLoad(_holder);
        }
        return _holder.transform;
    }

    [HarmonyPatch(typeof(CollectibleItem), nameof(CollectibleItem.Start))]
    private static class PickupPatch
    {
        [HarmonyPostfix]
        private static void After(CollectibleItem __instance)
        {
            try { OnPickupStart(__instance); }
            catch (Exception e) { AssetsLog.Error("Item model hook failed", e); }
        }
    }
}
