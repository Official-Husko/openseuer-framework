// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;

namespace ObenFramework.Housing;

/// <summary>One upgrade of the building or of one apartment, with the line it belongs to.</summary>
internal sealed class RenovationEntry
{
    public TenementUpgrade Upgrade;

    /// <summary>Null for the building's own upgrades.</summary>
    public TenementApartment Apartment;

    public TenementUpgrade[] Line;

    public RenovationInfo Info;
}

/// <summary>Walks the tenement's upgrade groups into <see cref="RenovationEntry"/>s and works out each one's state.</summary>
internal static class RenovationCatalog
{
    public static List<RenovationEntry> Build()
    {
        var entries = new List<RenovationEntry>();
        var controller = TenementController.instance;
        if (controller == null)
            return entries;
        var seen = new HashSet<string>();
        if (controller.general != null)
            AddGroups(entries, seen, null, GeneralGroups(controller.general));
        if (controller.apartments == null)
            return entries;
        foreach (var apartment in controller.apartments)
        {
            if (apartment != null && apartment.type != null)
                AddGroups(entries, seen, apartment, ApartmentGroups(apartment.type));
        }
        return entries;
    }

    public static RenovationEntry Find(string id)
    {
        if (!HousingRules.TryParseRenovationId(id, out _, out _))
            return null;
        foreach (var entry in Build())
        {
            if (entry.Info.Id == id)
                return entry;
        }
        return null;
    }

    public static TenementUpgradeProgress ProgressOf(RenovationEntry entry) => ProgressFor(entry.Upgrade, entry.Apartment);

    private static void AddGroups(List<RenovationEntry> entries, HashSet<string> seen, TenementApartment apartment, TenementUpgradeGroup[] groups)
    {
        int number = apartment != null ? apartment.apartmentNumber : 0;
        foreach (var group in groups)
        {
            if (group == null || group.updateGroup == null)
                continue;
            foreach (var upgrade in group.updateGroup)
            {
                try
                {
                    if (upgrade == null || string.IsNullOrEmpty(upgrade.Id))
                        continue;
                    string id = HousingRules.RenovationId(number, upgrade.Id);
                    if (!seen.Add(id))
                        continue;
                    var entry = new RenovationEntry { Upgrade = upgrade, Apartment = apartment, Line = group.updateGroup };
                    entry.Info = new RenovationInfo(id, upgrade.GetName(number), number, upgrade.GetPriceStartOC(), upgrade.GetPriceFinalRM(),
                        upgrade.GetRequiredTimeHours(), StateOf(entry));
                    entries.Add(entry);
                }
                catch (Exception e)
                {
                    GameEvents.Log?.Warn("A renovation could not be read: " + e.Message);
                }
            }
        }
    }

    private static TenementUpgradeGroup[] GeneralGroups(TenementGeneral g) => new[]
    {
        g.electricityUpgrades, g.waterUpgrades, g.heatingUpgrades, g.stairsUpgrades, g.elevatorUpgrades, g.facadeUpgrades, g.roofUpgrades,
        g.basementUpgrades, g.saunaUpgrades, g.shroomsUpgrades, g.workshopUpgrades, g.mineUpgrades, g.courtyardUpgrades, g.fenceUpgrades,
        g.saunaAndPierUpgrades, g.greenhouseUpgrades,
    };

    private static TenementUpgradeGroup[] ApartmentGroups(TenementApartmentType t) => new[]
    {
        t.electricity, t.water, t.heating, t.bathroom, t.walls, t.balcony, t.balcony2, t.kitchen,
    };

    private static TenementUpgradeProgress[] GeneralProgresses(TenementGeneral g) => new[]
    {
        g.electricityProgress, g.waterProgress, g.heatingProgress, g.stairsProgress, g.elevatorProgress, g.facadeProgress, g.roofProgress,
        g.basementProgress, g.saunaProgress, g.shroomsProgress, g.workshopProgress, g.mineProgress, g.courtyardProgress, g.fenceProgress,
        g.saunaAndPierProgress, g.greenhouseProgress,
    };

    private static RenovationState StateOf(RenovationEntry entry)
    {
        if (entry.Upgrade.IsUpgraded(entry.Apartment))
            return RenovationState.Installed;
        var progress = ProgressOf(entry);
        if (progress != null)
            return progress.upgradeReady ? RenovationState.AwaitingFinalPayment : RenovationState.InProgress;
        foreach (var sibling in entry.Line)
        {
            if (sibling != null && ProgressFor(sibling, entry.Apartment) != null)
                return RenovationState.Locked;
        }
        return entry.Upgrade.CanUpgrade(entry.Apartment) ? RenovationState.Available : RenovationState.Locked;
    }

    private static TenementUpgradeProgress ProgressFor(TenementUpgrade upgrade, TenementApartment apartment)
    {
        var controller = TenementController.instance;
        foreach (var progress in controller.currentUpgradeProgresses)
        {
            if (progress != null && Matches(progress, upgrade, apartment))
                return progress;
        }
        IEnumerable<TenementUpgradeProgress> slots = apartment == null ? GeneralProgresses(controller.general) : apartment.GetApartmentProgresses();
        foreach (var progress in slots)
        {
            if (progress != null && progress.upgradeReady && Matches(progress, upgrade, apartment))
                return progress;
        }
        return null;
    }

    private static bool Matches(TenementUpgradeProgress progress, TenementUpgrade upgrade, TenementApartment apartment)
    {
        // Unity fills unused progress slots with empty objects that have no upgrade.
        if (progress.upgrade == null || !TenementUpgrade.IsSameUpgrade(progress.upgrade, upgrade))
            return false;
        if (apartment == null)
            return progress.generalUpgrade;
        return progress.apartmentUpgrade && progress.apartmentNumber == apartment.apartmentNumber;
    }
}
