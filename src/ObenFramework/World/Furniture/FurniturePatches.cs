using System;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// The game's furniture entry points, as the world docs recommend them: <c>SetFurnitureInfo</c> is the
/// one call every placement makes, <c>FurniturePlaceable.Take</c> every removal (it recurses into
/// children and attached pieces, so each gets its own call). The build menu's place, take and move run as
/// the local player, which the patches record with <see cref="WorldChanges"/>.
/// </summary>
[HarmonyPatch]
internal static class FurniturePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FurniturePlaceable), nameof(FurniturePlaceable.SetFurnitureInfo))]
    private static void AfterSetInfo(FurniturePlaceable __instance)
    {
        try
        {
            // Loose items on surfaces have no furniture asset; they aren't furniture.
            if (__instance.furniture != null)
                FurnitureMoves.OnPlaced(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Furniture placed hook", e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.Place))]
    private static void BeforePlace()
    {
        WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);
        FurnitureMoves.BeginPlace();
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.Place))]
    private static Exception AfterPlace(Exception __exception)
    {
        try { FurnitureMoves.EndPlace(); }
        catch (Exception e) { WorldLog.Failed("Furniture place hook", e); }
        WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
        return __exception;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.Move))]
    private static void BeforeMove()
    {
        WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);
        FurnitureMoves.BeginMove();
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.Move))]
    private static Exception AfterMove(Exception __exception)
    {
        FurnitureMoves.EndMove();
        WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
        return __exception;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.Take), new[] { typeof(GameObject) }, new[] { ArgumentType.Out })]
    private static void BeforeTake() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.Take), new[] { typeof(GameObject) }, new[] { ArgumentType.Out })]
    private static Exception AfterTake(Exception __exception)
    {
        WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
        return __exception;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FurniturePlaceable), nameof(FurniturePlaceable.Take))]
    private static void BeforePlaceableTake(FurniturePlaceable __instance, out FurnitureMoves.Taken __state)
    {
        __state = default;
        try { __state = FurnitureMoves.BeforeTake(__instance); }
        catch (Exception e) { WorldLog.Failed("Furniture removed hook (before)", e); }
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(FurniturePlaceable), nameof(FurniturePlaceable.Take))]
    private static Exception AfterPlaceableTake(Exception __exception, FurniturePlaceable __instance, FurnitureMoves.Taken __state)
    {
        try
        {
            if (__exception == null)
                FurnitureMoves.AfterTake(__instance, __state);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Furniture removed hook (after)", e);
        }
        return __exception;
    }

    // Closing the menu or walking out of the zone is what ends a move the player abandoned.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingSystem), nameof(BuildingSystem.ToggleMenu))]
    private static void AfterToggleMenu() => Abandoned();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingArea), "Exit")]
    private static void AfterAreaExit() => Abandoned();

    private static void Abandoned()
    {
        try { FurnitureMoves.FlushPending(); }
        catch (Exception e) { WorldLog.Failed("Furniture menu hook", e); }
    }
}
