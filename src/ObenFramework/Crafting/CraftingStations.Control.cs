// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.World;

namespace ObenFramework.Crafting;

public static partial class CraftingStations
{
    /// <summary>
    /// Stops a still or similar station that is processing a batch with liquid output, the way opening its
    /// lid early does: the batch fails. Returns true when the station stopped.
    /// </summary>
    public static bool Stop(string stationId)
    {
        if (Refused() || !(Find(stationId) is manufacturingProcess station))
            return false;
        if (station.stage != manufacturingProcess.Stage.Processing || station.liquidRemaining.Count == 0)
            return false;

        using (WorldChanges.EnterMod())
        {
            station.OpenLid();
            if (station.stage == manufacturingProcess.Stage.Processing)
                return false;
            station.process?.Destroy();
        }
        return true;
    }

    /// <summary>Pauses the timer of a processing still or similar station. Returns false when it isn't processing or already paused.</summary>
    public static bool Pause(string stationId)
    {
        var station = Processing(stationId);
        if (station == null || station.process.stopped)
            return false;
        station.process.stopped = true;
        if (station.ManufactureSound != null)
            station.ManufactureSound.StopSound(destroySource: true);
        return true;
    }

    /// <summary>Resumes a paused station. Refuses while it has no power.</summary>
    public static bool Resume(string stationId)
    {
        var station = Processing(stationId);
        if (station == null || !station.process.stopped || !station.PoweredAndActive)
            return false;
        station.process.stopped = false;
        if (station.ManufactureSound != null)
            station.ManufactureSound.PlaySound(station.gameObject);
        return true;
    }

    /// <summary>
    /// Whether sunlight reaches the station, for recipes that need it; null when the station has no light
    /// check or the game hasn't worked it out yet.
    /// </summary>
    public static bool? HasNaturalLight(CraftingBase station)
    {
        if (station == null)
            return null;
        var checker = station.GetComponentInChildren<NaturalLightSourceChecker>();
        if (checker == null || SaveController.Loading || (TimeOfDayAzure.instance != null && TimeOfDayAzure.instance.updateTimeDisabled))
            return null;
        if (WindowNaturalLight.instance == null || !WindowNaturalLight.instance.initialized)
            return null;
        return checker.HasSun();
    }

    private static manufacturingProcess Processing(string stationId)
    {
        if (Refused() || !(Find(stationId) is manufacturingProcess station))
            return null;
        return station.stage == manufacturingProcess.Stage.Processing && station.process != null ? station : null;
    }
}
