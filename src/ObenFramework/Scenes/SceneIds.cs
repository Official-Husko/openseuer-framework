// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.World.Addressing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.Scenes;

/// <summary>
/// Short text ids for components of the active scene, used by the scene logic, signs and other modules
/// that hand out "an id for this thing" to mods and the dev bridge. An id is the object's save GUID when
/// it has one (most relays, counters, state machines and signs do), otherwise its
/// <see cref="ObjectAddress"/> text. <see cref="Find{T}"/> also takes a bare hierarchy path or
/// <c>#instanceId</c>. Lookups walk the scene, so don't call them every frame.
/// </summary>
public static class SceneIds
{
    /// <summary>The id of a component, or an empty string for null.</summary>
    public static string Of(Component component)
    {
        if (component == null)
            return "";
        return GuidIndex.GuidOf(component) ?? ObjectAddress.Of(component).ToString();
    }

    /// <summary>
    /// The component of type <typeparamref name="T"/> for an id from <see cref="Of"/>, an address, a GUID, a
    /// path or <c>#instanceId</c>. Returns null when nothing in the active scene matches.
    /// </summary>
    public static T Find<T>(string id) where T : Component
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (GuidIndex.Find(id) is T byGuid && InActiveScene(byGuid))
            return byGuid;

        var found = ObjectResolver.Find<T>(id);
        if (found != null)
            return found;

        // A GUID the index doesn't know (a component type outside SavableScript): look at the candidates.
        foreach (var candidate in Active<T>(true))
        {
            if (GuidIndex.GuidOf(candidate) == id)
                return candidate;
        }
        return null;
    }

    /// <summary>Every component of type <typeparamref name="T"/> in the active scene.</summary>
    public static List<T> Active<T>(bool includeInactive = false) where T : Component
    {
        var list = new List<T>();
        var inactive = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        foreach (var candidate in Object.FindObjectsByType<T>(inactive, FindObjectsSortMode.None))
        {
            if (InActiveScene(candidate))
                list.Add(candidate);
        }
        return list;
    }

    private static bool InActiveScene(Component component) =>
        component != null && component.gameObject.scene == SceneManager.GetActiveScene();
}
