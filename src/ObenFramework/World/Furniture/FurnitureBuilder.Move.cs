using System;
using Pathfinding;
using UnityEngine;

namespace ObenFramework.World;

public static partial class FurnitureBuilder
{
    /// <summary>
    /// Moves a piece to another spot, keeping its id. Raises <see cref="PlacedFurniture.FurnitureMoved"/>.
    /// A piece that stands on or hangs on another, or carries others, is refused: move the piece it belongs
    /// to, or remove and place it again.
    /// </summary>
    /// <param name="id">The piece's id.</param>
    /// <param name="position">New world position of the root.</param>
    /// <param name="rotation">New world rotation of the root; null keeps the current one.</param>
    /// <param name="modelRotation">New local rotation of the first child (the part the rotate key turns); null keeps it.</param>
    /// <param name="source">Who moves it, as the event reports it.</param>
    public static FurnitureEdit Move(string id, Vector3 position, Quaternion? rotation = null, Quaternion? modelRotation = null,
        ChangeSource source = ChangeSource.Mod)
    {
        var placeable = PlacedFurniture.Find(id);
        return placeable == null ? FurnitureEdit.Fail($"No furniture '{id}'.") : Move(placeable, position, rotation, modelRotation, source);
    }

    /// <inheritdoc cref="Move(string, Vector3, Quaternion?, Quaternion?, ChangeSource)"/>
    public static FurnitureEdit Move(FurniturePlaceable placeable, Vector3 position, Quaternion? rotation = null,
        Quaternion? modelRotation = null, ChangeSource source = ChangeSource.Mod)
    {
        string notReady = NotReady();
        if (notReady != null)
            return FurnitureEdit.Fail(notReady);
        if (!IsFinite(position) || (rotation.HasValue && !IsFinite(rotation.Value)) || (modelRotation.HasValue && !IsFinite(modelRotation.Value)))
            return FurnitureEdit.Fail("Position or rotation is NaN or infinite.");
        if (placeable == null || placeable.manager == null)
            return FurnitureEdit.Fail("That furniture is not standing in a zone.");
        if (placeable.cantTake || placeable.furniture == null || placeable.item != null)
            return FurnitureEdit.Fail($"{PlacedFurniture.Describe(placeable).Title} cannot be moved.");
        if (placeable.parent != null || placeable.attachedto != null)
            return FurnitureEdit.Fail("That furniture stands on or hangs on another piece; move that one.");
        if (PiecesOn(placeable).Count > 0)
            return FurnitureEdit.Fail("That furniture carries other pieces, which would stay behind.");

        try
        {
            var before = PlacedFurniture.Describe(placeable);
            var transform = placeable.transform;
            transform.position = position;
            if (rotation.HasValue)
                transform.rotation = rotation.Value;
            if (modelRotation.HasValue && transform.childCount > 0)
                transform.GetChild(0).localRotation = modelRotation.Value;
            Refresh(placeable);

            var after = PlacedFurniture.Describe(placeable);
            if (PlacedFurniture.AnySubscribers && WorldChanges.Active)
                PlacedFurniture.FurnitureMoved.Raise(new FurnitureMove(before, after, source));
            return FurnitureEdit.Ok(after);
        }
        catch (Exception e)
        {
            Fail("Moving furniture", e);
            return FurnitureEdit.Fail($"Moving the furniture failed: {e.Message}");
        }
    }

    // What the game updates after a piece changes place: window light and the walkable area around it.
    private static void Refresh(FurniturePlaceable placeable)
    {
        var light = placeable.GetComponentInChildren<NaturalLightSourceChecker>();
        if (light != null)
            light.RefreshModifiers();
        var cut = placeable.GetComponent<NavmeshCut>();
        if (cut != null)
            cut.ForceUpdate();
    }
}
