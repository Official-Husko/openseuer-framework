using System;
using System.Collections.Generic;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Items;

/// <summary>
/// Ids for scene components that survive sessions: the n-th component of a type inside a piece of furniture
/// the player placed is <c>furniture:{furnitureId}:{n}</c> (its prefab GUID is shared by every copy), anything
/// else is its game GUID or, without one, its <see cref="ObjectAddress"/> text. Used for containers, crafting
/// stations, plants and the other world objects the item and crafting modules list. Not for per-frame use.
/// Unlike <see cref="Scenes.SceneIds"/> it knows placed furniture and looks in every loaded scene (the
/// backpack storages live outside the active one).
/// </summary>
internal static class ObjectIds
{
    private const string FurniturePrefix = "furniture:";

    /// <summary>The id of a component. Pass the same <typeparamref name="T"/> to <see cref="Find{T}"/>.</summary>
    public static string IdOf<T>(T component) where T : Component
    {
        if (component == null)
            return null;

        var piece = component.GetComponentInParent<FurniturePlaceable>();
        if (piece != null && !piece.isPreplacedFurniture && piece.furniture != null)
        {
            var inside = piece.GetComponentsInChildren<T>(true);
            int index = Array.IndexOf(inside, component);
            if (index >= 0)
                return FurniturePrefix + PlacedFurniture.IdOf(piece) + ":" + index;
        }
        return GuidIndex.GuidOf(component) ?? ObjectAddress.Of(component).ToString();
    }

    /// <summary>The component with this id (or any text <see cref="ObjectResolver.Find{T}"/> understands), or null.</summary>
    public static T Find<T>(string id) where T : Component
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (id.StartsWith(FurniturePrefix, StringComparison.Ordinal))
        {
            int split = id.LastIndexOf(':');
            if (split <= FurniturePrefix.Length || !int.TryParse(id.Substring(split + 1), out int index) || index < 0)
                return null;
            var piece = PlacedFurniture.Find(id.Substring(FurniturePrefix.Length, split - FurniturePrefix.Length));
            if (piece == null)
                return null;
            var inside = piece.GetComponentsInChildren<T>(true);
            return index < inside.Length ? inside[index] : null;
        }
        return ObjectResolver.Find<T>(id);
    }

    /// <summary>The active components of a type in the loaded scenes. A scan.</summary>
    public static List<T> Active<T>() where T : Component
    {
        var result = new List<T>();
        foreach (var component in Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (component != null)
                result.Add(component);
        }
        return result;
    }
}
