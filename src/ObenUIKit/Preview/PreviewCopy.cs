using System.Collections.Generic;
using UnityEngine;

namespace ObenUIKit.Preview;

/// <summary>
/// Makes a look-only copy of an object for a model preview: meshes and their materials, nothing that
/// runs. The copy is made under an inactive holder, so none of the original's scripts ever wake up (a
/// game prefab's scripts would register themselves with saves, sounds or the HUD), and everything but
/// the transforms, meshes, mesh renderers and LOD groups is removed before it is switched on.
/// </summary>
internal static class PreviewCopy
{
    private static readonly List<Component> Parts = new List<Component>();

    public static GameObject Make(GameObject source, Transform parent, int layer)
    {
        var holder = new GameObject("ObenUIKit preview holder");
        holder.SetActive(false);
        try
        {
            var copy = Object.Instantiate(source, holder.transform, false);
            copy.name = source.name;
            Strip(copy);
            SetLayer(copy.transform, layer);
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.SetActive(true);
            return copy;
        }
        finally
        {
            Object.Destroy(holder);
        }
    }

    private static void Strip(GameObject copy)
    {
        // A component others depend on ([RequireComponent]) can only go after them: scripts first, then
        // the engine's components (particle systems before their renderers), then whatever renderers are left.
        for (int pass = 0; pass < 3; pass++)
        {
            Parts.Clear();
            copy.GetComponentsInChildren(true, Parts);
            for (int i = Parts.Count - 1; i >= 0; i--)
            {
                var part = Parts[i];
                if (part == null || Keep(part) || !DueIn(part, pass))
                    continue;
                Object.DestroyImmediate(part);
            }
        }
        Parts.Clear();

        foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skin)
                skin.updateWhenOffscreen = true;
        }
    }

    private static bool DueIn(Component part, int pass) => pass switch
    {
        0 => part is MonoBehaviour,
        1 => !(part is Renderer),
        _ => true,
    };

    private static bool Keep(Component part) =>
        part is Transform || part is MeshFilter || part is MeshRenderer || part is SkinnedMeshRenderer || part is LODGroup;

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayer(root.GetChild(i), layer);
    }
}
