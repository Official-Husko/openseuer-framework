// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Building;

/// <summary>The building zone the player is in (a <c>FurnitureManager</c>, see <c>FurnitureBuilder.Zones</c>).</summary>
public readonly struct BuildingAreaInfo
{
    public BuildingAreaInfo(FurnitureManager manager, string id, bool removeOnly, bool menuDisabled)
    {
        Manager = manager;
        Id = id;
        RemoveOnly = removeOnly;
        MenuDisabled = menuDisabled;
    }

    public FurnitureManager Manager { get; }

    /// <summary>The zone's GUID, as <c>FurnitureBuilder.FindZone</c> takes it.</summary>
    public string Id { get; }

    /// <summary>Pieces can only be taken here, not placed (a place the player doesn't own).</summary>
    public bool RemoveOnly { get; }

    /// <summary>The build menu is off here for now (the place isn't fully examined yet).</summary>
    public bool MenuDisabled { get; }
}

/// <summary>
/// Which building zone the player stands in, and when they walk into or out of one. Raised from the game's own
/// area triggers, so nothing is polled.
/// </summary>
public static class BuildingAreas
{
    private static FurnitureManager _last;

    /// <summary>The player walked into a building zone.</summary>
    public static readonly WorldEvent<BuildingAreaInfo> Entered = new WorldEvent<BuildingAreaInfo>(nameof(Entered));

    /// <summary>The player left a building zone (also when walking straight into the next one).</summary>
    public static readonly WorldEvent<BuildingAreaInfo> Exited = new WorldEvent<BuildingAreaInfo>(nameof(Exited));

    /// <summary>The zone the player is in, or null outside every zone.</summary>
    public static BuildingAreaInfo? Current
    {
        get
        {
            var manager = BuildingSystem.instance != null ? BuildingSystem.activeManager : null;
            return manager != null ? Describe(manager) : (BuildingAreaInfo?)null;
        }
    }

    public static BuildingAreaInfo Describe(FurnitureManager manager)
    {
        if (manager == null)
            throw new ArgumentNullException(nameof(manager));
        return new BuildingAreaInfo(manager, manager.GUID, manager.areaType == BuildingArea.AreaType.RemoveOnly, manager.BuildMenuDisabled);
    }

    /// <summary>Takes the zone as it is now as the starting point, without events (after a load or a reload).</summary>
    internal static void Seed() => _last = BuildingSystem.instance != null ? BuildingSystem.activeManager : null;

    internal static void Reset() => _last = null;

    private static void Sync()
    {
        var active = BuildingSystem.activeManager;
        var previous = _last;
        _last = active;
        if (previous == active || !WorldChanges.Active)
            return;
        if (previous != null && Exited.HasSubscribers)
            Exited.Raise(Describe(previous));
        if (active != null && Entered.HasSubscribers)
            Entered.Raise(Describe(active));
    }

    [HarmonyPatch(typeof(BuildingArea))]
    private static class AreaPatch
    {
        [HarmonyFinalizer]
        [HarmonyPatch(nameof(BuildingArea.Enter))]
        private static Exception AfterEnter(Exception __exception)
        {
            Guarded();
            return __exception;
        }

        [HarmonyFinalizer]
        [HarmonyPatch("Exit")]
        private static Exception AfterExit(Exception __exception)
        {
            Guarded();
            return __exception;
        }

        private static void Guarded()
        {
            try { Sync(); }
            catch (Exception e) { FurnitureRegistry.Log.Error("Building area hook", e); }
        }
    }
}
