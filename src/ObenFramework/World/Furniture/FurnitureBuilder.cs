using System;
using System.Collections.Generic;
using ObenFramework.Game;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// Places, moves and removes furniture from code, without the build menu and without touching the local
/// player's furniture inventory. This is what a mod uses to apply another player's change: the changes are
/// raised as <see cref="ChangeSource.Mod"/> (or the source you give), so a mod that forwards
/// <see cref="PlacedFurniture"/> events skips them and nothing is sent back.
/// </summary>
/// <remarks>
/// Placing makes the piece out of nothing, and removing destroys it; neither adds to or takes from
/// <c>BuildingSystem.availableFurnitures</c>, which is the local player's own stock. The calls do not check
/// whether the spot is free: that is the build menu's preview, not the game's rules. Pieces are named by the
/// ids of <see cref="PlacedFurniture"/>. Call on the main thread in a loaded world.
/// </remarks>
public static partial class FurnitureBuilder
{
    /// <summary>The building zones of the loaded scenes.</summary>
    public static List<FurnitureZone> Zones(string scene = null)
    {
        var zones = new List<FurnitureZone>();
        var building = BuildingSystem.instance;
        if (building == null)
            return zones;

        foreach (var manager in building.furnitureManagers)
        {
            if (manager == null)
                continue;
            string managerScene = manager.gameObject.scene.name;
            if (!string.IsNullOrEmpty(scene) && managerScene != scene)
                continue;
            zones.Add(new FurnitureZone(manager.GUID, managerScene, manager.transform.position, manager.placedFurniture.Count,
                manager == BuildingSystem.activeManager));
        }
        return zones;
    }

    /// <summary>The zone with this GUID, or null.</summary>
    public static FurnitureManager FindZone(string id)
    {
        var building = BuildingSystem.instance;
        if (building == null || string.IsNullOrEmpty(id))
            return null;

        foreach (var manager in building.furnitureManagers)
        {
            if (manager != null && manager.GUID == id)
                return manager;
        }
        return null;
    }

    // Shared by the three calls: nothing is changed while a save loads or without a building system.
    private static string NotReady()
    {
        if (BuildingSystem.instance == null)
            return "The building system is not available (no world is loaded).";
        if (WorldState.IsLoadingSave)
            return "A save is loading.";
        return null;
    }

    private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

    private static bool IsFinite(Quaternion q) => IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);

    private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    private static void Fail(string where, Exception e) => WorldLog.Failed(where, e);
}
