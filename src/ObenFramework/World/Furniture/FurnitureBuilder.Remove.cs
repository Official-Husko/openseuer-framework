// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.World;

public static partial class FurnitureBuilder
{
    // The relay the game fires when a piece is taken (it switches things in the scene); private.
    private static readonly AccessTools.FieldRef<FurniturePlaceable, Relay> OnTaken =
        AccessTools.FieldRefAccess<FurniturePlaceable, Relay>("onTaken");

    /// <summary>
    /// Removes a piece from the world. Unlike the build menu's take, nothing goes into the local player's
    /// furniture inventory.
    /// </summary>
    /// <param name="id">The piece's id (see <see cref="PlacedFurniture.IdOf"/>).</param>
    /// <param name="source">Who removes it, as the <see cref="PlacedFurniture.FurnitureRemoved"/> event reports it.</param>
    /// <param name="withPieces">
    /// Also remove what stands on it or hangs on it, which the game would put into the inventory with it.
    /// Each gets its own event, those first. Without it a piece that carries others is refused.
    /// </param>
    /// <param name="force">
    /// Remove it although it holds something (items in a drawer, liquid, crafting progress, fuel), which is
    /// lost. Without it such a piece is refused.
    /// </param>
    public static FurnitureEdit Remove(string id, ChangeSource source = ChangeSource.Mod, bool withPieces = false, bool force = false)
    {
        var placeable = PlacedFurniture.Find(id);
        return placeable == null ? FurnitureEdit.Fail($"No furniture '{id}'.") : Remove(placeable, source, withPieces, force);
    }

    /// <inheritdoc cref="Remove(string, ChangeSource, bool, bool)"/>
    public static FurnitureEdit Remove(FurniturePlaceable placeable, ChangeSource source = ChangeSource.Mod, bool withPieces = false, bool force = false)
    {
        string notReady = NotReady();
        if (notReady != null)
            return FurnitureEdit.Fail(notReady);
        if (placeable == null || placeable.manager == null)
            return FurnitureEdit.Fail("That furniture is not standing in a zone.");

        try
        {
            var chain = new List<FurniturePlaceable>();
            if (!Collect(placeable, withPieces, chain, out string refusal))
                return FurnitureEdit.Fail(refusal);
            foreach (var piece in chain)
            {
                if (!force && HoldsThings(piece))
                    return FurnitureEdit.Fail($"{PlacedFurniture.Describe(piece).Title} holds something; pass force to remove it anyway.");
            }

            // Children come before what they stand on, like the game's own take.
            var removed = default(FurnitureInfo);
            foreach (var piece in chain)
                removed = RemoveOne(piece, source);
            return FurnitureEdit.Ok(removed);
        }
        catch (Exception e)
        {
            Fail("Removing furniture", e);
            return FurnitureEdit.Fail($"Removing the furniture failed: {e.Message}");
        }
    }

    // The pieces to remove, children first and the asked-for piece last.
    private static bool Collect(FurniturePlaceable piece, bool withPieces, List<FurniturePlaceable> into, out string refusal)
    {
        refusal = null;
        if (piece.cantTake)
        {
            refusal = $"{PlacedFurniture.Describe(piece).Title} cannot be taken.";
            return false;
        }
        var carried = piece.GetFurnitureInfo();
        if (piece.item != null || (carried != null && carried.itemStack != null) || piece.furniture == null)
        {
            refusal = "That is a loose item, not furniture; remove it with FloorItems.";
            return false;
        }

        foreach (var other in PiecesOn(piece))
        {
            if (!withPieces)
            {
                refusal = $"{PlacedFurniture.Describe(piece).Title} carries other furniture; pass withPieces to remove it too.";
                return false;
            }
            if (!into.Contains(other) && !Collect(other, true, into, out refusal))
                return false;
        }
        into.Add(piece);
        return true;
    }

    private static List<FurniturePlaceable> PiecesOn(FurniturePlaceable piece)
    {
        var found = new List<FurniturePlaceable>();
        foreach (var info in piece.manager.AllFurnitures)
        {
            if (info == null || info.gameObject == null)
                continue;
            var other = info.gameObject.GetComponent<FurniturePlaceable>();
            if (other != null && other != piece && (other.parent == piece || other.attachedto == piece))
                found.Add(other);
        }
        return found;
    }

    private static bool HoldsThings(FurniturePlaceable piece)
    {
        var trash = piece.GetComponentInChildren<FurnitureTrash>();
        if (trash != null && trash.items != null && trash.items.Count > 0)
            return true;
        return BuildingSystem.instance.HasSaveableContent(piece.gameObject);
    }

    // What FurniturePlaceable.Take does, minus the call that moves the piece into the player's inventory.
    private static FurnitureInfo RemoveOne(FurniturePlaceable placeable, ChangeSource source)
    {
        var before = PlacedFurniture.Describe(placeable);
        var go = placeable.gameObject;
        var manager = placeable.manager;

        InteractableChair.StandUpFrom(go);
        if (placeable.parent != null)
            placeable.parent.RemoveChildFurniture(placeable);
        if (placeable.attachedto != null)
            placeable.attachedto.RemoveAttachedFurniture(placeable);

        var onTaken = OnTaken(placeable);
        if (onTaken != null)
            onTaken.triggerOutputs();
        manager.TookFurniture(go);
        foreach (var action in placeable.GetComponentsInChildren<StorageJanitorAction>())
            action.RemoveJanitorAction();
        foreach (var action in placeable.GetComponentsInChildren<RelayJanitor>())
            action.RemoveJanitorAction();

        UnityEngine.Object.Destroy(go);
        placeable.Destroyed();
        manager.placedFurniture.RemoveAll(entry => entry == null || entry.gameObject == go);
        PlacedFurniture.Forget(placeable);

        if (PlacedFurniture.AnySubscribers && WorldChanges.Active)
            PlacedFurniture.FurnitureRemoved.Raise(new FurnitureChange(before, source));
        return before;
    }
}
