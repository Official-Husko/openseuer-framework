// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>Growing stations (grow boxes, planters, beds): listing their spots, pausing growth and switching rot.</summary>
public static class GrowingStations
{
    public static List<GrowingStationInfo> All()
    {
        var list = new List<GrowingStationInfo>();
        foreach (var station in SceneIds.Active<Growing>())
            list.Add(Describe(station));
        return list;
    }

    public static Growing Find(string id) => CraftingStations.Find(id) as Growing;

    public static GrowingStationInfo Describe(Growing station)
    {
        var planted = new List<GrowingSpotInfo>();
        foreach (var spot in station.GrowingSlots)
        {
            if (spot == null || !spot.HasPlant())
                continue;
            var plant = spot.Plant.GetComponent<grow>();
            bool pickable = plant == null && spot.Plant.GetComponentInChildren<CollectibleItem>() != null;
            planted.Add(new GrowingSpotInfo(SceneIds.IdOf(spot), plant != null ? Plants.IdOf(plant) : null, pickable));
        }
        return new GrowingStationInfo(CraftingStations.IdOf(station), station.transform.position, station.GrowingSlots.Count, station.paused,
            station.ManualHarvest, planted);
    }

    /// <summary>Pauses or resumes growth in the whole station. Refused while a save loads.</summary>
    public static bool SetPaused(string id, bool paused)
    {
        var station = Find(id);
        if (station == null || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (paused)
                station.PauseGrowth();
            else
                station.UnpauseGrowth();
        }
        return station.paused == paused;
    }

    /// <summary>Starts or stops the plants of a station rotting (what a cut power or water supply does).</summary>
    public static bool SetRotting(string id, bool rotting)
    {
        var station = Find(id);
        if (station == null || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (rotting)
                station.StartRot();
            else
                station.StopRot();
        }
        return true;
    }
}
