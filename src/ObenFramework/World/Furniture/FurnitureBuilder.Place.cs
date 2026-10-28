using System;
using UnityEngine;

namespace ObenFramework.World;

public static partial class FurnitureBuilder
{
    /// <summary>
    /// Places a piece of furniture, as the build menu's place does but without using up a piece from the
    /// player's stock.
    /// </summary>
    /// <param name="kind">The furniture: its id (asset path), short name or title, see <see cref="FurnitureKinds.Find"/>.</param>
    /// <param name="position">World position of the piece's root.</param>
    /// <param name="rotation">World rotation of the root.</param>
    /// <param name="zone">GUID of the building zone (see <see cref="Zones"/>); null uses the zone the local player is building in.</param>
    /// <param name="options">Skin, alternative prefab, model rotation, the id to use and parent links.</param>
    /// <param name="source">Who is placing it, as the <see cref="PlacedFurniture.FurniturePlaced"/> event reports it.</param>
    public static FurnitureEdit Place(string kind, Vector3 position, Quaternion rotation, string zone = null,
        FurniturePlacement options = default, ChangeSource source = ChangeSource.Mod)
    {
        string notReady = NotReady();
        if (notReady != null)
            return FurnitureEdit.Fail(notReady);
        if (!IsFinite(position) || !IsFinite(rotation) || (options.ModelRotation.HasValue && !IsFinite(options.ModelRotation.Value)))
            return FurnitureEdit.Fail("Position or rotation is NaN or infinite.");

        var furniture = FurnitureKinds.Find(kind);
        if (furniture == null)
            return FurnitureEdit.Fail($"Unknown furniture '{kind}'.");

        var manager = string.IsNullOrEmpty(zone) ? BuildingSystem.activeManager : FindZone(zone);
        if (manager == null)
            return FurnitureEdit.Fail(string.IsNullOrEmpty(zone) ? "No zone given and the player is not in a building area." : $"Unknown zone '{zone}'.");

        if (options.Skin < 0 || options.Skin > furniture.skins.Length)
            return FurnitureEdit.Fail($"{furniture.title} has {furniture.skins.Length} skins, not {options.Skin}.");
        if (options.Alternative < 0 || options.Alternative > furniture.AlternativePrefabs.Length)
            return FurnitureEdit.Fail($"{furniture.title} has {furniture.AlternativePrefabs.Length} alternatives, not {options.Alternative}.");

        FurniturePlaceable parent = null, anchor = null;
        if (!string.IsNullOrEmpty(options.ParentId) && (parent = PlacedFurniture.Find(options.ParentId)) == null)
            return FurnitureEdit.Fail($"No furniture '{options.ParentId}' to stand on.");
        if (!string.IsNullOrEmpty(options.AttachedToId) && (anchor = PlacedFurniture.Find(options.AttachedToId)) == null)
            return FurnitureEdit.Fail($"No furniture '{options.AttachedToId}' to attach to.");
        if (!string.IsNullOrEmpty(options.Id) && PlacedFurniture.Find(options.Id) != null)
            return FurnitureEdit.Fail($"The id {options.Id} belongs to another piece of furniture.");

        var prefab = furniture.GetPrefab(options.Alternative);
        if (prefab == null)
            return FurnitureEdit.Fail($"{furniture.title} has no prefab for alternative {options.Alternative}.");

        GameObject go = null;
        FurniturePlaceable placeable = null;
        try
        {
            go = UnityEngine.Object.Instantiate(prefab, position, rotation, manager.transform);
            placeable = go.GetComponent<FurniturePlaceable>();
            if (placeable == null)
            {
                UnityEngine.Object.Destroy(go);
                return FurnitureEdit.Fail($"{furniture.title}'s prefab is not placeable furniture.");
            }

            // The model rotation and the links are set before the game's placement call, so the event it
            // causes already describes the finished piece.
            if (options.ModelRotation.HasValue && go.transform.childCount > 0)
                go.transform.GetChild(0).localRotation = options.ModelRotation.Value;
            if (!string.IsNullOrEmpty(options.Id))
                PlacedFurniture.Adopt(placeable, options.Id);
            if (parent != null)
                parent.AddChildFurniture(placeable);
            if (anchor != null)
                anchor.AddAttachedFurniture(placeable);

            var meta = new BuildingSystem.FurnitureInfo.Meta { currentSkin = options.Skin, currentAlternativePrefab = options.Alternative };
            var info = new BuildingSystem.FurnitureInfo(furniture, meta, Furniture.CreateTaskItem(furniture, meta), null);
            using (WorldChanges.Enter(source))
            {
                placeable.SetFurnitureInfo(info, manager);
                placeable.UpdateMeta();
                placeable.UpdateStorageStatus();
            }
            return FurnitureEdit.Ok(PlacedFurniture.Describe(placeable));
        }
        catch (Exception e)
        {
            Fail("Placing furniture", e);
            if (go != null)
                Discard(manager, placeable, go);
            return FurnitureEdit.Fail($"Placing {furniture.title} failed: {e.Message}");
        }
    }

    // A half-made piece: out of the zone's list and the links, then gone.
    private static void Discard(FurnitureManager manager, FurniturePlaceable placeable, GameObject go)
    {
        try
        {
            manager.placedFurniture.RemoveAll(entry => entry == null || entry.gameObject == go);
            if (placeable != null)
            {
                if (placeable.parent != null)
                    placeable.parent.RemoveChildFurniture(placeable);
                if (placeable.attachedto != null)
                    placeable.attachedto.RemoveAttachedFurniture(placeable);
                PlacedFurniture.Forget(placeable);
            }
        }
        finally
        {
            UnityEngine.Object.Destroy(go);
        }
    }
}
