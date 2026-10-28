// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Building;

/// <summary>
/// Puts a mod's mesh onto a copy of a furniture prefab. The game rotates child 0 of every piece, so the model
/// goes under it, centred on the template's visible bounds and standing on their base, at its own size. The
/// template's renderers under child 0 are hidden and its solid colliders swapped for boxes around the model on
/// the same objects, so interactions (sitting, storage) still find their colliders.
/// </summary>
internal static class FurnitureModelFit
{
    private const float MinEdge = 0.02f;

    public static bool Apply(GameObject root, Mesh mesh, Material[] materials, Bounds[] boxes, string id)
    {
        if (root == null || mesh == null)
            return false;
        if (root.transform.childCount == 0)
        {
            FurnitureRegistry.Log.Warn("fit " + id + " " + root.name, "the prefab has no child 0; the template's look is kept");
            return false;
        }

        var target = root.transform.GetChild(0);
        if (!VisibleBounds(target, out var template))
        {
            FurnitureRegistry.Log.Warn("fit " + id + " " + root.name, "the prefab shows no mesh under child 0; the template's look is kept");
            return false;
        }

        var solids = new List<Collider>();
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (collider.enabled && !collider.isTrigger)
                solids.Add(collider);
        }
        int layer = solids.Count > 0 ? solids[0].gameObject.layer : target.gameObject.layer;

        var part = new GameObject("Mod model");
        part.layer = layer;
        part.transform.SetParent(target, false);
        var lossy = target.lossyScale;
        var scale = new Vector3(SafeInverse(lossy.x), SafeInverse(lossy.y), SafeInverse(lossy.z));
        part.transform.localScale = scale;
        var model = mesh.bounds;
        part.transform.localPosition = new Vector3(
            template.center.x - model.center.x * scale.x,
            template.min.y - model.min.y * scale.y,
            template.center.z - model.center.z * scale.z);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterials = materials;

        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.gameObject != part)
                renderer.enabled = false;
        }

        // One box per object that had a solid collider, so each keeps its role; the mod's own boxes all go on the
        // first of them under child 0 (or child 0 itself).
        var owners = new List<Transform>();
        var sources = new List<Collider>();
        foreach (var collider in solids)
        {
            collider.enabled = false;
            if (!owners.Contains(collider.transform))
            {
                owners.Add(collider.transform);
                sources.Add(collider);
            }
        }
        if (boxes != null && boxes.Length > 0)
        {
            int index = owners.FindIndex(t => t == target || t.IsChildOf(target));
            var owner = index >= 0 ? owners[index] : target;
            var source = index >= 0 ? sources[index] : null;
            foreach (var box in boxes)
                AddBox(owner, source, BoxIn(part.transform, box, owner));
        }
        else
        {
            if (owners.Count == 0)
            {
                owners.Add(target);
                sources.Add(null);
            }
            for (int i = 0; i < owners.Count; i++)
                AddBox(owners[i], sources[i], BoxIn(part.transform, model, owners[i]));
        }
        return true;
    }

    private static void AddBox(Transform owner, Collider source, Bounds bounds)
    {
        var box = owner.gameObject.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        if (source != null)
            box.sharedMaterial = source.sharedMaterial;
    }

    // The union of the enabled meshes under child 0, in child 0's space.
    private static bool VisibleBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (!renderer.enabled || filter == null || filter.sharedMesh == null)
                continue;
            var part = Transformed(filter.sharedMesh.bounds, renderer.transform, target);
            if (found)
                bounds.Encapsulate(part);
            else
                bounds = part;
            found = true;
        }
        return found;
    }

    // A box given in the model's space, around its corners as seen from the owner.
    private static Bounds BoxIn(Transform part, Bounds model, Transform owner)
    {
        var result = Transformed(model, part, owner);
        result.size = Vector3.Max(result.size, Vector3.one * MinEdge);
        return result;
    }

    private static Bounds Transformed(Bounds local, Transform from, Transform to)
    {
        var min = local.min;
        var max = local.max;
        var result = new Bounds(to.InverseTransformPoint(from.TransformPoint(min)), Vector3.zero);
        for (int i = 1; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            result.Encapsulate(to.InverseTransformPoint(from.TransformPoint(corner)));
        }
        return result;
    }

    private static float SafeInverse(float value) => Mathf.Abs(value) < 1e-6f ? 1f : 1f / value;
}
