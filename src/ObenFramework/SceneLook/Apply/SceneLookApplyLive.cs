// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>
/// Re-skins a live NPC: applies the look like on any body, then keeps the NPC's own renderers hidden with a
/// <see cref="SceneLookLiveGuard"/> on the look's holder, since the game switches them back on now and then.
/// </summary>
internal static class SceneLookApplyLive
{
    public static bool Reskin(string modId, SceneLookLook look, GameObject npc, bool skeleton, out string reason)
    {
        if (npc == null)
        {
            reason = "no npc";
            return false;
        }
        var own = Shown(npc);
        var ownIds = Ids(own);
        var before = Children(npc);
        if (!SceneLookApply.Apply(modId, look, npc, skeleton, out reason))
            return false;

        int holderId = SceneLookLivePlan.Holder(before, Children(npc));
        var holder = holderId == 0 ? null : Find(npc, holderId);
        if (holder == null)
        {
            SceneLooks.Log.Warn("reskin holder " + modId, "the look was applied but its holder wasn't found, so the NPC's own renderers aren't kept hidden");
            return true;
        }
        var kept = SceneLookLivePlan.Watch(ownIds, Objects(npc));
        if (kept.Count == 0)
            return true;
        var watch = new List<GameObject>(kept.Count);
        foreach (int index in kept)
            watch.Add(own[index]);
        holder.AddComponent<SceneLookLiveGuard>().Setup(watch);
        return true;
    }

    // The objects carrying the NPC's own visible skinned renderers before the look went on.
    private static List<GameObject> Shown(GameObject npc)
    {
        var shown = new List<GameObject>();
        foreach (var renderer in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer != null && renderer.enabled && !shown.Contains(renderer.gameObject))
                shown.Add(renderer.gameObject);
        }
        return shown;
    }

    private static List<int> Ids(List<GameObject> objects)
    {
        var ids = new List<int>(objects.Count);
        foreach (var item in objects)
            ids.Add(item.GetInstanceID());
        return ids;
    }

    private static List<int> Children(GameObject npc)
    {
        var root = npc.transform;
        var ids = new List<int>(root.childCount);
        for (int i = 0; i < root.childCount; i++)
            ids.Add(root.GetChild(i).gameObject.GetInstanceID());
        return ids;
    }

    private static List<int> Objects(GameObject npc)
    {
        var transforms = npc.GetComponentsInChildren<Transform>(true);
        var ids = new List<int>(transforms.Length);
        foreach (var transform in transforms)
            ids.Add(transform.gameObject.GetInstanceID());
        return ids;
    }

    private static GameObject Find(GameObject npc, int id)
    {
        foreach (var transform in npc.GetComponentsInChildren<Transform>(true))
        {
            if (transform.gameObject.GetInstanceID() == id)
                return transform.gameObject;
        }
        return null;
    }
}
