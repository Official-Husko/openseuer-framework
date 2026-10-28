// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>
/// Finds a player shop's shelves in the loaded scenes: the containers inside every piece of furniture of its
/// building area, then its single containers. A scan, done when a shop is listed or sells, never per frame.
/// </summary>
internal static class PlayerShopShelves
{
    public static List<Storage> Of(PlayerShopRecord record)
    {
        var shelves = new List<Storage>();
        if (record == null)
            return shelves;

        var area = Area(record.Area);
        if (area != null)
        {
            foreach (var piece in area.AllFurnitures)
            {
                if (piece == null || piece.gameObject == null)
                    continue;
                foreach (var storage in piece.gameObject.GetComponentsInChildren<Storage>(true))
                    Add(shelves, storage);
            }
        }
        foreach (string id in record.Containers)
            Add(shelves, Containers.Find(id));
        return shelves;
    }

    /// <summary>The shelf with this container id, or null when it isn't one of the shop's loaded shelves.</summary>
    public static Storage Find(PlayerShopRecord record, string containerId)
    {
        foreach (var shelf in Of(record))
        {
            if (Containers.IdOf(shelf) == containerId)
                return shelf;
        }
        return null;
    }

    private static FurnitureManager Area(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return null;
        foreach (var manager in Object.FindObjectsByType<FurnitureManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (manager.GUID == guid)
                return manager;
        }
        return null;
    }

    // Only containers that have started have slots; a trader's storage is never a shelf.
    private static void Add(List<Storage> shelves, Storage storage)
    {
        if (storage != null && storage.Slots != null && storage.GetComponent<Trade>() == null && !shelves.Contains(storage))
            shelves.Add(storage);
    }
}
